using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Threading;
using ApexHMI.Interfaces;
using ApexHMI.Models;
using ApexHMI.Services;

namespace ApexHMI.ViewModels;

public partial class MainViewModel
{
    protected MainViewModel(
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
    {
        _opcUaService = opcUaService;
        _csvImportService = csvImportService;
        _xmlImportService = xmlImportService;
        _ioTableImportService = ioTableImportService;
        _ioProgramGenerationService = ioProgramGenerationService;
        _configurationService = configurationService;
        _namingRulesService = namingRulesService;
        _designerLayoutService = designerLayoutService;
        _designerProjectService = designerProjectService;
        _parameterService = parameterService;
        _alarmService = alarmService;
        _flowLogCsvService = flowLogCsvService;
        _recipeService = recipeService;
        _trendHistoryService = trendHistoryService;
        _gitPullService = gitPullService;
        _generatedArtifactSyncService = generatedArtifactSyncService;

        BuildNavigation();
        BindingOperations.EnableCollectionSynchronization(IoTableRows, _ioTableRowsSync);
        BindingOperations.EnableCollectionSynchronization(GeneratedIoPrograms, _generatedIoProgramsSync);
        BindingOperations.EnableCollectionSynchronization(ManualCylinderBlocks, _manualCylinderBlocksSync);
        BindingOperations.EnableCollectionSynchronization(Logs, _logsSync);
        DesignerElements.CollectionChanged += DesignerElements_CollectionChanged;
        DesignerPages.CollectionChanged += DesignerPages_CollectionChanged;
        Parameters.CollectionChanged += Parameters_CollectionChanged;
        IoTableRows.CollectionChanged += (_, _) =>
        {
            RefreshIoGenerationSummary();
            OnPropertyChanged(nameof(CanSaveIoTable));
        };
        ManualCylinderBlocks.CollectionChanged += ManualCylinderBlocks_CollectionChanged;
        GeneratedIoPrograms.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasGeneratedIoPrograms));
        GeneratedAutoPrograms.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasGeneratedAutoPrograms));
        AutoProgramFlowNodes.CollectionChanged += (_, _) => RefreshAutoProgramSummary();
        AlarmHistory.CollectionChanged += (_, _) => RefreshAlarmStatistics();
        CurrentAlarms.CollectionChanged += (_, _) => RefreshAlarmStatistics();
        FlowSteps.CollectionChanged += FlowSteps_CollectionChanged;
        TrendSamples.CollectionChanged += (_, _) => RefreshTrendVisuals();
        ManualCylinderBlocksView = CollectionViewSource.GetDefaultView(ManualCylinderBlocks);
        ManualCylinderBlocksView.SortDescriptions.Add(new SortDescription(nameof(ManualCylinderBlockItem.DisplayOrder), ListSortDirection.Ascending));
        ManualCylinderBlocksView.SortDescriptions.Add(new SortDescription(nameof(ManualCylinderBlockItem.CylinderIndex), ListSortDirection.Ascending));
        _subscriptionTimer = new DispatcherTimer();
        _subscriptionTimer.Interval = TimeSpan.FromMilliseconds(200);
        _subscriptionTimer.Tick += async (_, _) => await AutoRefreshTickAsync();
        _opcUaBrowserRefreshTimer = new DispatcherTimer();
        _opcUaBrowserRefreshTimer.Interval = TimeSpan.FromMilliseconds(1000);
        _opcUaBrowserRefreshTimer.Tick += async (_, _) => await AutoRefreshSelectedOpcUaNodeTickAsync();
        _opcUaService.TagValueChanged += OpcUaService_TagValueChanged;
        SeedDemoData();
        SeedDesignerData();
        EnsureDefaultManualCylinderBlock();
        EnsureDefaultManualAxisBlock();
        SeedParameters();
        ParametersView.Filter = FilterParameterItem;
        RefreshParameterPermissions();
        RefreshAlarmStatistics();
        RefreshMonitorView();
        SeedFlowSteps();
        SeedRecipes();
        SeedTrendSamples();
        RefreshFlowView();
        RefreshFlowIssueSummaries();
        RebuildProgramMonitorTraceHistory();
        SeedAutoProgramFlow();
        InitializeRobotControl();
        _ = InitializeAsync();
    }

    private void InitializeRobotControl()
    {
        RobotControlViewModel = new RobotControlViewModel(1, "EPSON机械手 #1");
        RobotControlViewModel.Robot.Status.Ready = true;
        RobotControlViewModel.Robot.Status.MotorsOn = true;
        RobotControlViewModel.Robot.Status.PowerHigh = true;
        RobotControlViewModel.Robot.Command.Speed = 50;
        RobotControlViewModel.Robot.Command.PointNum = 0;
        RobotControlViewModel.Robot.Command.ProductType = 1;
    }

    private async Task InitializeAsync()
    {
        try
        {
            await LoadNamingRulesAsync();
            await LoadConfigAsync();
            await LoadParametersAsync();
            await ConnectAsync();
        }
        catch (Exception ex)
        {
            SystemMessage = $"启动加载配置失败：{ex.Message}";
            AddLog("配置", SystemMessage, "Error");
        }
    }

    private void FlowSteps_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            RebuildProgramMonitorTraceHistory();
            RefreshProgramMonitorTrace();
            return;
        }

        if (e.NewItems is not null)
        {
            AppendProgramMonitorTraceHistory(e.NewItems.OfType<FlowStepRecord>());
        }

        RefreshProgramMonitorTrace();
    }
}
