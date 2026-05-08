using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ApexHMI.Interfaces;
using ApexHMI.Models.RuntimeUi;
using ApexHMI.Services;
using ApexHMI.Services.DataBinding;
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
    private readonly WidgetBlockGenerator _widgetBlockGenerator;
    private readonly ManualPageAutoGenerator _manualPageAutoGenerator;

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
        WidgetBlockGenerator widgetBlockGenerator,
        ManualPageAutoGenerator manualPageAutoGenerator,
        IUserService userService,
        PlcVariableImportService plcVariableImportService)
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
        _widgetBlockGenerator = widgetBlockGenerator;
        _manualPageAutoGenerator = manualPageAutoGenerator;

        Home = new HomeViewModel(this);
        Monitor = new MonitorViewModel(this);
        Manual = new ManualViewModel(this);
        ParametersModule = new ParameterViewModel(this, parameterService);
        Alarm = new AlarmViewModel(this, alarmService);
        Recipe = new RecipeViewModel(this, recipeService);
        GitPull = new GitPullViewModel(this, gitPullService, generatedArtifactSyncService);
        Login = new LoginViewModel(this);
        Audit = new AuditViewModel(this);

        SetGitPullViewModel(GitPull);

        Recipe.SeedRecipes();

        // 先初始化运行时（LoadDefault 设置 Current），再让编辑器共享同一 ProjectDocument
        InitializeDynamicRuntime();
        DesignerEditor = new DesignerEditorViewModel(this, _projectEditorService, _widgetEditorService, runtimeProjectService, _widgetBlockGenerator, _widgetFactory, plcVariableImportService);
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

    public DynamicPageHostViewModel RuntimePage { get; private set; } = null!;

    /// <summary>专用于 Tab 3 「手动操作」的 DynamicPageHost；独立于 Tab 10，
    /// 这样手动子页签切换不会污染 Tab 10 当前页。</summary>
    public DynamicPageHostViewModel ManualPage { get; private set; } = null!;

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

    /// <summary>P3.4 运行时全屏：true 时主窗口隐藏导航/状态栏，仅显示 DynamicPageHost。</summary>
    [ObservableProperty]
    private bool _isRuntimeFullScreen;

    [RelayCommand]
    private void ToggleRuntimeFullScreen() => IsRuntimeFullScreen = !IsRuntimeFullScreen;

    /// <summary>
    /// Tab 3 「手动操作」是否使用设计器布局（路径 B）。
    /// true：显示 ManualPage DynamicPageHost，按子页签加载 manual.* 页面，
    ///       内容来自 IO 导入自动生成或用户编辑后的 ProjectDocument。
    /// false（默认）：显示原有硬编码 ManualView UI。
    /// </summary>
    [ObservableProperty]
    private bool _useDesignerManualLayout;

    /// <summary>当前 Tab 3 子页签 → 对应 manual.* 页面 RouteKey 的映射。</summary>
    private static string ResolveManualRouteKey(string subSection) => subSection switch
    {
        "气缸" => Services.RuntimeUi.ManualPageAutoGenerator.CylindersRouteKey,
        "轴"   => Services.RuntimeUi.ManualPageAutoGenerator.AxesRouteKey,
        "机械手" => Services.RuntimeUi.ManualPageAutoGenerator.RobotsRouteKey,
        "挡停" => Services.RuntimeUi.ManualPageAutoGenerator.StoppersRouteKey,
        _ => string.Empty
    };

    /// <summary>根据当前手动子页签加载对应的 manual.* 页到 ManualPage（设计器布局开关下使用）。</summary>
    internal async Task LoadManualPageForCurrentSubSectionAsync()
    {
        if (!UseDesignerManualLayout) return;
        var routeKey = ResolveManualRouteKey(CurrentManualSubSection);
        if (string.IsNullOrEmpty(routeKey)) return;

        var project = _runtimeProjectService.Current;
        if (project is null) return;

        var page = project.Pages.FirstOrDefault(p =>
            string.Equals(p.RouteKey, routeKey, StringComparison.OrdinalIgnoreCase));
        if (page is null) return;

        ManualPage.LoadPage(page);
        await _dataBindingService.AttachAsync(ManualPage);
    }

    partial void OnUseDesignerManualLayoutChanged(bool value)
    {
        if (value)
            _ = LoadManualPageForCurrentSubSectionAsync();
    }

    /// <summary>
    /// IO 导入后调用：自动生成/刷新 manual.* 系列页面，并保存工程。
    /// </summary>
    internal void RegenerateManualPages()
    {
        var project = _runtimeProjectService.Current;
        if (project is null) return;
        try
        {
            _manualPageAutoGenerator.GenerateAll(
                project,
                ManualCylinderBlockCards,
                ManualAxisBlockCards,
                RobotControlViewModel is not null,
                Tags);
            _runtimeProjectService.Save(project);
            RefreshTopNavUserPages();
            Log.Information("MainWindowViewModel: 手动页面已根据 IO 重新生成");
        }
        catch (System.Exception ex)
        {
            Log.Error(ex, "RegenerateManualPages 失败");
        }
    }

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
            // NavigateCommand 中会先清空 override；用户页跳转后再覆盖回父段，
            // 让侧栏继续显示父段（如"手动操作"）的子项。
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
        ManualPage  = new DynamicPageHostViewModel(_widgetFactory, HandleRuntimeAction, this);
        // 顶部页面标签栏：点击切换 Tab 10 当前页
        RuntimePage.RequestLoadPage = key => _ = NavigateToRuntimePageAsync(key);
        ManualPage.RequestLoadPage  = key => _ = NavigateToRuntimePageAsync(key);
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
                _ = NavigateToRuntimePageAsync(actionParam);
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
            await _dataBindingService.WriteAsync(parts[0], boolValue);
        }
    }

    private async Task HandleWriteIntAsync(string actionParam)
    {
        var parts = actionParam.Split('|');
        if (parts.Length >= 2 && int.TryParse(parts[1], out var v))
            await _dataBindingService.WriteAsync(parts[0], v);
    }

    private async Task HandleWriteFloatAsync(string actionParam)
    {
        var parts = actionParam.Split('|');
        if (parts.Length >= 2 && double.TryParse(parts[1],
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out var v))
            await _dataBindingService.WriteAsync(parts[0], v);
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
