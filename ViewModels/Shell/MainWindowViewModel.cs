using ApexHMI.Interfaces;
using ApexHMI.Services;
using ApexHMI.ViewModels.Modules;

namespace ApexHMI.ViewModels.Shell;

public sealed class MainWindowViewModel : MainViewModel
{
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
        GeneratedArtifactSyncService generatedArtifactSyncService)
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
        Home = new HomeViewModel(this);
        Monitor = new MonitorViewModel(this);
        Manual = new ManualViewModel(this);
        ParametersModule = new ParameterViewModel(this);
        Alarm = new AlarmViewModel(this);
        Recipe = new RecipeViewModel(this);
        Designer = new DesignerViewModel(this);
        Login = new LoginViewModel(this);
        Audit = new AuditViewModel(this);
    }

    public HomeViewModel Home { get; }
    public MonitorViewModel Monitor { get; }
    public ManualViewModel Manual { get; }
    public ParameterViewModel ParametersModule { get; }
    public AlarmViewModel Alarm { get; }
    public RecipeViewModel Recipe { get; }
    public DesignerViewModel Designer { get; }
    public LoginViewModel Login { get; }
    public AuditViewModel Audit { get; }
}
