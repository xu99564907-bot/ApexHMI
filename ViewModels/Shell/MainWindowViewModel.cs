using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ApexHMI.Interfaces;
using ApexHMI.Models.RuntimeUi;
using ApexHMI.Services;
using ApexHMI.Services.DataBinding;
using ApexHMI.Services.Production;
using ApexHMI.Services.RuntimeUi;
using ApexHMI.Services.Security;
using ApexHMI.Models.RuntimeUi;
using ApexHMI.ViewModels.Modules;
using ApexHMI.ViewModels.Runtime;
using Serilog;

namespace ApexHMI.ViewModels.Shell;

public sealed partial class MainWindowViewModel : MainViewModel
{
    private readonly RuntimeProjectService _runtimeProjectService;
    private readonly RuntimeDataBindingService _dataBindingService;
    private readonly IWidgetViewFactory _widgetFactory;
    private readonly IProjectEditorService _projectEditorService;
    private readonly IWidgetEditorService _widgetEditorService;
    private readonly SimulationService _simulationService;
    private readonly IAuditService _auditService;
    private readonly RecipeJobCoordinator _recipeJobCoordinator;
    private readonly SessionManager _sessionManager;
    private readonly PasswordPolicy _passwordPolicy;
    private readonly AccountLockoutService _accountLockout;

    /// <summary>M3.2: 默认 PLC 写入确认超时 = 3 秒。</summary>
    private static readonly System.TimeSpan WriteConfirmTimeout = System.TimeSpan.FromSeconds(3);

    public MainWindowViewModel(
        IOpcUaService opcUaService,
        CsvImportService csvImportService,
        XmlImportService xmlImportService,
        IoTableImportService ioTableImportService,
        IoProgramGenerationService ioProgramGenerationService,
        IConfigurationService configurationService,
        NamingRulesService namingRulesService,
        IParameterService parameterService,
        IAlarmService alarmService,
        FlowLogCsvService flowLogCsvService,
        IRecipeService recipeService,
        TrendHistoryService trendHistoryService,
        GitPullService gitPullService,
        GeneratedArtifactSyncService generatedArtifactSyncService,
        IDataPointCatalog dataPointCatalog,
        IWidgetViewFactory widgetFactory,
        RuntimeProjectService runtimeProjectService,
        RuntimeDataBindingService dataBindingService,
        IProjectEditorService projectEditorService,
        IWidgetEditorService widgetEditorService,
        IUserService userService,
        PlcVariableImportService plcVariableImportService,
        IProductionCountService productionCountService,
        SimulationService simulationService,
        IAuditService auditService,
        RecipeJobCoordinator recipeJobCoordinator,
        SessionManager sessionManager,
        PasswordPolicy passwordPolicy,
        AccountLockoutService accountLockout)
        : base(
            opcUaService,
            csvImportService,
            xmlImportService,
            ioTableImportService,
            ioProgramGenerationService,
            configurationService,
            namingRulesService,
            parameterService,
            alarmService,
            flowLogCsvService,
            recipeService,
            trendHistoryService,
            gitPullService,
            generatedArtifactSyncService,
            userService)
    {
        _runtimeProjectService = runtimeProjectService;
        _dataBindingService = dataBindingService;
        _widgetFactory = widgetFactory;
        _projectEditorService = projectEditorService;
        _widgetEditorService = widgetEditorService;
        _simulationService = simulationService;
        _auditService = auditService;
        _recipeJobCoordinator = recipeJobCoordinator;
        _sessionManager = sessionManager;
        _passwordPolicy = passwordPolicy;
        _accountLockout = accountLockout;
        // M5.2: 订阅会话超时事件 → 强制注销 + 提示
        _sessionManager.SessionExpired += OnSessionExpired;
        // M5.2: 在角色变化时联动会话管理器（登入 / 注销）
        this.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(LoginUser))
            {
                var user = LoginUser;
                if (string.IsNullOrEmpty(user) || user == "操作员")
                {
                    _sessionManager.Logout();
                }
                else
                {
                    _sessionManager.Login(user);
                }
                OnPropertyChanged(nameof(SessionRemainingText));
            }
        };
        // M3.2/M4.3: 把内存 sink 注入到 AuditService —— 后端（CSV 或 SQLite）+ 内存 OperationAudits 同时收到记录
        Action<string, string, string, string> sink = (action, target, result, detail) => AddAudit(action, target, result, detail);
        switch (_auditService)
        {
            case AuditServiceSqlite sqlite: sqlite.AttachMemorySink(sink); break;
            case AuditService csv: csv.AttachMemorySink(sink); break;
        }

        Home = new HomeViewModel(this);
        Monitor = new MonitorViewModel(this);
        Manual = new ManualViewModel(this);
        ParametersModule = new ParameterViewModel(this, parameterService);
        Alarm = new AlarmViewModel(this, alarmService);
        Recipe = new RecipeViewModel(this, recipeService);
        GitPull = new GitPullViewModel(this, gitPullService, generatedArtifactSyncService);
        Login = new LoginViewModel(this);
        Audit = new AuditViewModel(this);
        Count = new CountViewModel(this, productionCountService);

        SetGitPullViewModel(GitPull);

        Recipe.SeedRecipes();

        // 先初始化运行时（LoadDefault 设置 Current），再让编辑器共享同一 ProjectDocument
        InitializeDynamicRuntime();
        DesignerEditor = new DesignerEditorViewModel(this, _projectEditorService, _widgetEditorService, runtimeProjectService, _widgetFactory, plcVariableImportService);
        Designer = new DesignerViewModel(this);
    }

    public HomeViewModel Home { get; }
    public MonitorViewModel Monitor { get; }
    public ManualViewModel Manual { get; }
    public ParameterViewModel ParametersModule { get; }
    public AlarmViewModel Alarm { get; }
    public RecipeViewModel Recipe { get; }
    public DesignerEditorViewModel DesignerEditor { get; }
    public DesignerViewModel Designer { get; }
    public GitPullViewModel GitPull { get; }
    public LoginViewModel Login { get; }
    public AuditViewModel Audit { get; }
    public CountViewModel Count { get; }

    public DynamicPageHostViewModel RuntimePage { get; private set; } = null!;

    public ProjectDocument? RuntimeProject => _runtimeProjectService.Current;

    /// <summary>
    /// 用户挂载到主导航的页面列表（按 NavOrder 排序）。
    /// MainWindow.xaml 顶栏会动态渲染按钮，点击后切换到运行页 + 加载该页面。
    /// </summary>
    public IEnumerable<PageDefinition> TopNavUserPages
    {
        get
        {
            var project = _runtimeProjectService.Current;
            if (project is null) return System.Linq.Enumerable.Empty<PageDefinition>();
            return project.Pages
                .Where(p => p.ShowInTopNav)
                .OrderBy(p => p.NavOrder)
                .ThenBy(p => p.Title);
        }
    }

    /// <summary>编辑器/发布触发后调用：刷新顶栏用户页按钮。</summary>
    internal void RefreshTopNavUserPages()
    {
        OnPropertyChanged(nameof(TopNavUserPages));
        RefreshSidebarUserPages();
    }

    /// <summary>
    /// 把工程中 ParentRouteKey 等于内置导航段（"手动操作"/"监控"/...）的用户页面，
    /// 作为子项注入到对应 NavigationItem.Children；点击按 RouteKey 跳转到运行页 Tab。
    /// </summary>
    internal void RefreshSidebarUserPages()
    {
        var project = _runtimeProjectService.Current;
        foreach (var nav in NavigationItems)
        {
            // 移除上一次注入的用户页项（RouteKey 非 null）
            for (var i = nav.Children.Count - 1; i >= 0; i--)
            {
                if (nav.Children[i].RouteKey is not null)
                    nav.Children.RemoveAt(i);
            }

            if (project is null) continue;

            var injected = project.Pages
                .Where(p => string.Equals(p.ParentRouteKey, nav.Title, StringComparison.Ordinal))
                .OrderBy(p => p.NavOrder)
                .ThenBy(p => p.Title);
            foreach (var p in injected)
                nav.Children.Add(new NavigationItemViewModel(p.Title, p.RouteKey, nav.Title));
        }
    }

    /// <summary>M5.2: 公开会话/策略/锁定服务供子 ViewModel 反射访问 + 状态栏绑定。</summary>
    public SessionManager SessionManager => _sessionManager;
    public PasswordPolicy PasswordPolicy => _passwordPolicy;
    public AccountLockoutService AccountLockout => _accountLockout;
    public RecipeJobCoordinator RecipeJobCoordinator => _recipeJobCoordinator;
    public RuntimeDataBindingService DataBindingService => _dataBindingService;
    public IAuditService AuditService => _auditService;

    /// <summary>M5.2: 顶部状态栏可绑定的"距会话超时 X 分钟"。</summary>
    public string SessionRemainingText
    {
        get
        {
            var r = _sessionManager.Remaining;
            if (_sessionManager.CurrentUser is null) return string.Empty;
            if (r <= TimeSpan.Zero) return "会话已超时";
            return r.TotalMinutes >= 1
                ? $"距会话超时 {(int)r.TotalMinutes} 分钟"
                : $"距会话超时 {(int)r.TotalSeconds} 秒";
        }
    }

    private void OnSessionExpired(string user)
    {
        // SessionManager 的 DispatcherTimer 已在 UI 线程触发
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            SystemMessage = "会话超时，请重新登录";
            try { _ = _auditService.LogOperationAsync(user, "session-expired", user, true, $"timeout={_sessionManager.Timeout.TotalMinutes:F0}min"); } catch { }
            LogoutCommand.Execute(null);
            System.Windows.MessageBox.Show(
                $"用户 {user} 已无操作 {_sessionManager.Timeout.TotalMinutes:F0} 分钟，会话超时已自动注销。请重新登录。",
                "会话超时", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            OnPropertyChanged(nameof(SessionRemainingText));
        });
    }

    /// <summary>P3.4 运行时全屏：true 时主窗口隐藏导航/状态栏，仅显示 DynamicPageHost。</summary>
    [ObservableProperty]
    private bool _isRuntimeFullScreen;

    /// <summary>P10F 离线模拟模式开关。开启时 SimulationService 注入假数据。</summary>
    [ObservableProperty]
    private bool _isSimulationMode;

    partial void OnIsSimulationModeChanged(bool value)
    {
        if (value) _simulationService.Start();
        else _simulationService.Stop();
    }

    [RelayCommand]
    private void ToggleRuntimeFullScreen() => IsRuntimeFullScreen = !IsRuntimeFullScreen;

    [RelayCommand]
    private async Task NavigateToUserPage(string? routeKey)
    {
        if (string.IsNullOrWhiteSpace(routeKey)) return;
        await NavigateToRuntimePageAsync(routeKey);
        NavigateCommand.Execute("运行页面");
    }

    /// <summary>
    /// 侧栏导航按钮统一入口：若 NavigationItem 携带 RouteKey（用户画布页），
    /// 跳转到运行页 Tab 并加载该页；否则按 Title 走固定段 Navigate 逻辑。
    /// </summary>
    [RelayCommand]
    private async Task NavigateNavItem(ApexHMI.ViewModels.NavigationItemViewModel? item)
    {
        if (item is null) return;
        if (!string.IsNullOrWhiteSpace(item.RouteKey))
        {
            await NavigateToRuntimePageAsync(item.RouteKey!);
            NavigateCommand.Execute("运行页面");
            // 把 CurrentSection 改为子项 Title (如"电批操作")，让子导航 active trigger 命中
            // 同时 SetNavigationGroupOverride(ParentTitle) 让主导航也保持高亮
            CurrentSection = item.Title;
            SetNavigationGroupOverride(item.ParentTitle);
            // 从侧栏父段进入用户画布页时，不显示运行页顶部的"页面切换"按钮条 —
            // 那些跳转应当只通过侧栏进行。
            RuntimePage.SetAvailablePages(System.Linq.Enumerable.Empty<Models.RuntimeUi.PageDefinition>());
        }
        else
        {
            NavigateCommand.Execute(item.Title);
        }
    }

    private void InitializeDynamicRuntime()
    {
        RuntimePage = new DynamicPageHostViewModel(_widgetFactory, HandleRuntimeAction, this);
        // 顶部页面标签栏：点击切换 Tab 10 当前页
        RuntimePage.RequestLoadPage = key => _ = NavigateToRuntimePageAsync(key);
        var project = _runtimeProjectService.LoadDefault();
        var defaultPage = project.Pages.FirstOrDefault(p =>
            string.Equals(p.RouteKey, project.DefaultPageRouteKey, StringComparison.OrdinalIgnoreCase))
                          ?? project.Pages.FirstOrDefault();
        if (defaultPage is not null)
        {
            _ = NavigateToRuntimePageAsync(defaultPage.RouteKey);
        }
        RefreshSidebarUserPages();
    }

    private void HandleRuntimeAction(string actionType, string actionParam)
    {
        switch (actionType)
        {
            case "navigate":
                {
                    // 优先匹配工程内用户页（按 RouteKey）；否则当作固定段名（主界面/手动操作/参数设定...）
                    var project = _runtimeProjectService.Current;
                    var isUserPage = project?.Pages.Any(p =>
                        string.Equals(p.RouteKey, actionParam, StringComparison.OrdinalIgnoreCase)) == true;
                    if (isUserPage) _ = NavigateToRuntimePageAsync(actionParam);
                    else NavigateCommand.Execute(actionParam);
                }
                break;
            case "write-bool":
                _ = HandleWriteBoolAsync(actionParam);
                break;
            case "write-int":
                _ = HandleWriteIntAsync(actionParam);
                break;
            case "write-float":
                _ = HandleWriteFloatAsync(actionParam);
                break;
            case "write-string":
                _ = HandleWriteStringAsync(actionParam);
                break;
            case "show-dialog":
                System.Windows.MessageBox.Show(actionParam, "提示");
                break;
        }
    }

    private async Task HandleWriteBoolAsync(string actionParam)
    {
        var parts = actionParam.Split('|');
        if (parts.Length >= 2 && bool.TryParse(parts[1], out var boolValue))
        {
            await WriteWithConfirmAndAuditAsync(parts[0], boolValue, "write-bool");
        }
    }

    private async Task HandleWriteIntAsync(string actionParam)
    {
        var parts = actionParam.Split('|');
        if (parts.Length >= 2 && int.TryParse(parts[1], out var v))
            await WriteWithConfirmAndAuditAsync(parts[0], v, "write-int");
    }

    private async Task HandleWriteFloatAsync(string actionParam)
    {
        var parts = actionParam.Split('|');
        if (parts.Length >= 2 && double.TryParse(parts[1],
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out var v))
            await WriteWithConfirmAndAuditAsync(parts[0], v, "write-float");
    }

    /// <summary>P10C: 字符串写入。actionParam 格式：tag|value（value 内允许含 | 时取首段后剩余全部）。</summary>
    private async Task HandleWriteStringAsync(string actionParam)
    {
        if (string.IsNullOrEmpty(actionParam)) return;
        var idx = actionParam.IndexOf('|');
        if (idx <= 0 || idx >= actionParam.Length - 1) return;
        var tag = actionParam.Substring(0, idx);
        var value = actionParam.Substring(idx + 1);
        await WriteWithConfirmAndAuditAsync(tag, value, "write-string");
    }

    /// <summary>
    /// M3.2: 统一 PLC 写入入口 — 同步等待确认 + 超时 + 失败 SystemMessage + 审计。
    /// 替换 fire-and-forget；WinCC 真实行为是写完才返回。
    /// </summary>
    private async Task WriteWithConfirmAndAuditAsync(string tagId, object value, string actionLabel)
    {
        _sessionManager.KeepAlive(); // M5.2: 写 PLC 算用户活动
        var result = await _dataBindingService.WriteAsyncWithConfirm(tagId, value, WriteConfirmTimeout);
        var detail = $"value={value} elapsedMs={result.Elapsed.TotalMilliseconds:F0}" +
                     (result.Success ? string.Empty :
                      result.TimedOut ? " timeout" :
                      $" err={result.ErrorMessage}");

        if (!result.Success)
        {
            SystemMessage = result.TimedOut
                ? $"写入超时：{tagId}（{WriteConfirmTimeout.TotalSeconds:F0}s）"
                : $"写入失败：{tagId} {result.ErrorMessage}";
            Log.Warning("Write 失败 tag={Tag} value={Value} detail={Detail}", tagId, value, detail);
        }

        try
        {
            await _auditService.LogOperationAsync(LoginUser, actionLabel, tagId, result.Success, detail);
        }
        catch (System.Exception ex)
        {
            Log.Debug(ex, "审计写入失败 tag={Tag}", tagId);
        }
    }

    internal async Task NavigateToRuntimePageAsync(string routeKey)
    {
        try
        {
            var project = _runtimeProjectService.Current;
            if (project is null) return;

            if (!RoleBasedAccessGuard.CanNavigateTo(CurrentUserRole, project, routeKey, out var reason))
            {
                SystemMessage = reason ?? "权限不足，无法访问该页面";
                Log.Warning("运行时导航被阻止 routeKey={RouteKey} reason={Reason}", routeKey, reason);
                return;
            }

            var page = project.Pages.FirstOrDefault(p =>
                string.Equals(p.RouteKey, routeKey, StringComparison.OrdinalIgnoreCase));
            if (page is null) return;

            // 注入模板页（P3.1）
            if (!string.IsNullOrWhiteSpace(project.TemplatePageRouteKey))
            {
                RuntimePage.TemplatePage = project.Pages.FirstOrDefault(p =>
                    string.Equals(p.RouteKey, project.TemplatePageRouteKey, StringComparison.OrdinalIgnoreCase));
            }
            RuntimePage.LoadPage(page);
            // 同步顶部页签可用列表（按角色过滤）
            var visiblePages = RoleBasedAccessGuard.FilterAccessible(CurrentUserRole, project.Pages)
                .Where(p => !string.Equals(p.RouteKey, project.TemplatePageRouteKey, StringComparison.OrdinalIgnoreCase));
            RuntimePage.SetAvailablePages(visiblePages);
            await _dataBindingService.AttachAsync(RuntimePage);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "NavigateToRuntimePage 失败 routeKey={RouteKey}", routeKey);
        }
    }

    [RelayCommand]
    private async Task ReloadRuntimeProject()
    {
        var project = _runtimeProjectService.LoadDefault();
        var defaultPage = project.Pages.FirstOrDefault(p =>
            string.Equals(p.RouteKey, project.DefaultPageRouteKey, StringComparison.OrdinalIgnoreCase))
                          ?? project.Pages.FirstOrDefault();
        if (defaultPage is not null)
        {
            await NavigateToRuntimePageAsync(defaultPage.RouteKey);
        }
        RefreshTopNavUserPages();
    }

    /// <summary>
    /// 编辑器"发布"时调用：用 Current（编辑器共享引用）刷新运行时视图，无需重读文件。
    /// </summary>
    internal async Task PublishProjectAsync()
    {
        var project = _runtimeProjectService.Current;
        if (project is null) return;

        var defaultPage = project.Pages.FirstOrDefault(p =>
            string.Equals(p.RouteKey, project.DefaultPageRouteKey, StringComparison.OrdinalIgnoreCase))
            ?? project.Pages.FirstOrDefault();

        if (defaultPage is not null)
            await NavigateToRuntimePageAsync(defaultPage.RouteKey);

        RefreshTopNavUserPages();
    }
}
