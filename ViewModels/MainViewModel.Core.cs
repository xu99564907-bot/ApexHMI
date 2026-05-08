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
using ApexHMI.Services.Security;
using Serilog;

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
        IParameterService parameterService,
        IAlarmService alarmService,
        FlowLogCsvService flowLogCsvService,
        IRecipeService recipeService,
        TrendHistoryService trendHistoryService,
        GitPullService gitPullService,
        GeneratedArtifactSyncService generatedArtifactSyncService,
        IUserService userService)
    {
        _opcUaService = opcUaService;
        _csvImportService = csvImportService;
        _xmlImportService = xmlImportService;
        _ioTableImportService = ioTableImportService;
        _ioProgramGenerationService = ioProgramGenerationService;
        _configurationService = configurationService;
        _namingRulesService = namingRulesService;
        _parameterService = parameterService;
        _alarmService = alarmService;
        _flowLogCsvService = flowLogCsvService;
        _recipeService = recipeService;
        _trendHistoryService = trendHistoryService;
        _gitPullService = gitPullService;
        _generatedArtifactSyncService = generatedArtifactSyncService;
        _userService = userService;

        BuildNavigation();
        BindingOperations.EnableCollectionSynchronization(IoTableRows, _ioTableRowsSync);
        BindingOperations.EnableCollectionSynchronization(GeneratedIoPrograms, _generatedIoProgramsSync);
        BindingOperations.EnableCollectionSynchronization(ManualCylinderBlocks, _manualCylinderBlocksSync);
        BindingOperations.EnableCollectionSynchronization(Logs, _logsSync);
        Parameters.CollectionChanged += Parameters_CollectionChanged;
        IoTableRows.CollectionChanged += (_, _) =>
        {
            RefreshIoGenerationSummary();
            OnPropertyChanged(nameof(CanSaveIoTable));
        };
        ManualCylinderBlocks.CollectionChanged += ManualCylinderBlocks_CollectionChanged;
        GeneratedIoPrograms.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasGeneratedIoPrograms));
        AlarmHistory.CollectionChanged += (_, _) => RefreshAlarmStatistics();
        CurrentAlarms.CollectionChanged += (_, _) => RefreshAlarmStatistics();
        FlowSteps.CollectionChanged += FlowSteps_CollectionChanged;
        TrendSamples.CollectionChanged += (_, _) => RefreshTrendVisuals();
        ManualCylinderBlocksView = CollectionViewSource.GetDefaultView(ManualCylinderBlocks);
        ManualCylinderBlocksView.SortDescriptions.Add(new SortDescription(nameof(ManualCylinderBlockItem.DisplayOrder), ListSortDirection.Ascending));
        ManualCylinderBlocksView.SortDescriptions.Add(new SortDescription(nameof(ManualCylinderBlockItem.CylinderIndex), ListSortDirection.Ascending));
        _subscriptionTimer = new DispatcherTimer();
        _subscriptionTimer.Interval = TimeSpan.FromMilliseconds(200);
        _subscriptionTimer.Tick += SubscriptionTimer_Tick;
        _opcUaBrowserRefreshTimer = new DispatcherTimer();
        _opcUaBrowserRefreshTimer.Interval = TimeSpan.FromMilliseconds(1000);
        _opcUaBrowserRefreshTimer.Tick += OpcUaBrowserRefreshTimer_Tick;
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
        InitializeRobotControl();
        _ = InitializeAsync();
    }

    private async void SubscriptionTimer_Tick(object? sender, EventArgs e)
    {
        try
        {
            await AutoRefreshTickAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "SubscriptionTimer_Tick 异常");
        }
    }

    private async void OpcUaBrowserRefreshTimer_Tick(object? sender, EventArgs e)
    {
        try
        {
            await AutoRefreshSelectedOpcUaNodeTickAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "OpcUaBrowserRefreshTimer_Tick 异常");
        }
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
