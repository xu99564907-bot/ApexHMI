using CommunityToolkit.Mvvm.Input;
using ApexHMI.Interfaces;
using ApexHMI.Models.RuntimeUi;
using ApexHMI.Services;
using ApexHMI.Services.DataBinding;
using ApexHMI.Services.RuntimeUi;
using ApexHMI.ViewModels.Modules;
using ApexHMI.ViewModels.Runtime;
using Serilog;

namespace ApexHMI.ViewModels.Shell;

public sealed partial class MainWindowViewModel : MainViewModel
{
    private readonly RuntimeProjectService _runtimeProjectService;
    private readonly RuntimeDataBindingService _dataBindingService;
    private readonly IWidgetViewFactory _widgetFactory;

    public MainWindowViewModel(
        IOpcUaService opcUaService,
        CsvImportService csvImportService,
        XmlImportService xmlImportService,
        IoTableImportService ioTableImportService,
        IoProgramGenerationService ioProgramGenerationService,
        IConfigurationService configurationService,
        NamingRulesService namingRulesService,
        DesignerLayoutService designerLayoutService,
        DesignerProjectService designerProjectService,
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
        RuntimeDataBindingService dataBindingService)
        : base(
            opcUaService,
            csvImportService,
            xmlImportService,
            ioTableImportService,
            ioProgramGenerationService,
            configurationService,
            namingRulesService,
            designerLayoutService,
            designerProjectService,
            parameterService,
            alarmService,
            flowLogCsvService,
            recipeService,
            trendHistoryService,
            gitPullService,
            generatedArtifactSyncService)
    {
        _runtimeProjectService = runtimeProjectService;
        _dataBindingService = dataBindingService;
        _widgetFactory = widgetFactory;

        Home = new HomeViewModel(this);
        Monitor = new MonitorViewModel(this);
        Manual = new ManualViewModel(this);
        ParametersModule = new ParameterViewModel(this, parameterService);
        Alarm = new AlarmViewModel(this, alarmService);
        Recipe = new RecipeViewModel(this, recipeService);
        Designer = new DesignerViewModel(this);
        GitPull = new GitPullViewModel(this, gitPullService, generatedArtifactSyncService);
        Login = new LoginViewModel(this);
        Audit = new AuditViewModel(this);

        SetGitPullViewModel(GitPull);

        Recipe.SeedRecipes();

        InitializeDynamicRuntime();
    }

    public HomeViewModel Home { get; }
    public MonitorViewModel Monitor { get; }
    public ManualViewModel Manual { get; }
    public ParameterViewModel ParametersModule { get; }
    public AlarmViewModel Alarm { get; }
    public RecipeViewModel Recipe { get; }
    public DesignerViewModel Designer { get; }
    public GitPullViewModel GitPull { get; }
    public LoginViewModel Login { get; }
    public AuditViewModel Audit { get; }

    public DynamicPageHostViewModel RuntimePage { get; private set; } = null!;

    public ProjectDocument? RuntimeProject => _runtimeProjectService.Current;

    private void InitializeDynamicRuntime()
    {
        RuntimePage = new DynamicPageHostViewModel(_widgetFactory, HandleRuntimeAction);
        var project = _runtimeProjectService.LoadDefault();
        var defaultPage = project.Pages.FirstOrDefault(p =>
            string.Equals(p.RouteKey, project.DefaultPageRouteKey, StringComparison.OrdinalIgnoreCase))
                          ?? project.Pages.FirstOrDefault();
        if (defaultPage is not null)
        {
            _ = NavigateToRuntimePageAsync(defaultPage.RouteKey);
        }
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

    private async Task NavigateToRuntimePageAsync(string routeKey)
    {
        try
        {
            var project = _runtimeProjectService.Current;
            if (project is null) return;

            var page = project.Pages.FirstOrDefault(p =>
                string.Equals(p.RouteKey, routeKey, StringComparison.OrdinalIgnoreCase));
            if (page is null) return;

            RuntimePage.LoadPage(page);
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
    }
}
