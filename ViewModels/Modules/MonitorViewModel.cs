using System.Collections.ObjectModel;
using System.ComponentModel;
using ApexHMI.Models;
using CommunityToolkit.Mvvm.Input;

namespace ApexHMI.ViewModels.Modules;

public sealed class MonitorViewModel : ModuleViewModelBase
{
    public MonitorViewModel(MainViewModel shell)
        : base(shell, "监控")
    {
        RefreshTagsCommand = new AsyncRelayCommand(() => Shell.RefreshTagsCommand.ExecuteAsync(null));
        LoadTrendHistoryCommand = new AsyncRelayCommand(() => Shell.LoadTrendHistoryCommand.ExecuteAsync(null));
        ImportFlowCsvCommand = new AsyncRelayCommand(() => Shell.ImportFlowCsvCommand.ExecuteAsync(null));
        LoadOpcUaBrowserRootCommand = new AsyncRelayCommand(() => Shell.LoadOpcUaBrowserRootCommand.ExecuteAsync(null));
        RefreshSelectedOpcUaNodeCommand = new AsyncRelayCommand(() => Shell.RefreshSelectedOpcUaNodeCommand.ExecuteAsync(null));
        AddSelectedOpcUaNodeAsTagCommand = new RelayCommand(() => Shell.AddSelectedOpcUaNodeAsTagCommand.Execute(null));
        SwitchIoMonitorTypeCommand = new RelayCommand<string?>(value => Shell.SwitchIoMonitorTypeCommand.Execute(value));
        IoMonitorPageUpCommand = new RelayCommand(() => Shell.IoMonitorPageUpCommand.Execute(null));
        IoMonitorPageDownCommand = new RelayCommand(() => Shell.IoMonitorPageDownCommand.Execute(null));
        PauseProgramMonitorTraceCommand = new RelayCommand(() => Shell.PauseProgramMonitorTraceCommand.Execute(null));
        ResumeProgramMonitorTraceCommand = new RelayCommand(() => Shell.ResumeProgramMonitorTraceCommand.Execute(null));
        EnterProgramMonitorTraceReplayCommand = new RelayCommand(() => Shell.EnterProgramMonitorTraceReplayCommand.Execute(null));
        ReturnProgramMonitorTraceToRealtimeCommand = new RelayCommand(() => Shell.ReturnProgramMonitorTraceToRealtimeCommand.Execute(null));
        ExportProgramMonitorTraceCsvCommand = new AsyncRelayCommand(() => Shell.ExportProgramMonitorTraceCsvCommand.ExecuteAsync(null));
        SaveProgramMonitorTraceSessionCommand = new AsyncRelayCommand(() => Shell.SaveProgramMonitorTraceSessionCommand.ExecuteAsync(null));
        LoadProgramMonitorTraceSessionCommand = new AsyncRelayCommand(() => Shell.LoadProgramMonitorTraceSessionCommand.ExecuteAsync(null));
        LocateProgramMonitorTraceTimeCommand = new RelayCommand(() => Shell.LocateProgramMonitorTraceTimeCommand.Execute(null));
        JumpToAlarmPageCommand = new RelayCommand(() => Shell.JumpToAlarmPageCommand.Execute(null));
        JumpToAuditPageCommand = new RelayCommand(() => Shell.JumpToAuditPageCommand.Execute(null));
        ExportFlowIssueReportCommand = new AsyncRelayCommand(() => Shell.ExportFlowIssueReportCommand.ExecuteAsync(null));
    }

    public IAsyncRelayCommand RefreshTagsCommand { get; }
    public IAsyncRelayCommand LoadTrendHistoryCommand { get; }
    public IAsyncRelayCommand ImportFlowCsvCommand { get; }
    public IAsyncRelayCommand LoadOpcUaBrowserRootCommand { get; }
    public IAsyncRelayCommand RefreshSelectedOpcUaNodeCommand { get; }
    public IRelayCommand AddSelectedOpcUaNodeAsTagCommand { get; }
    public IRelayCommand<string?> SwitchIoMonitorTypeCommand { get; }
    public IRelayCommand IoMonitorPageUpCommand { get; }
    public IRelayCommand IoMonitorPageDownCommand { get; }
    public IRelayCommand PauseProgramMonitorTraceCommand { get; }
    public IRelayCommand ResumeProgramMonitorTraceCommand { get; }
    public IRelayCommand EnterProgramMonitorTraceReplayCommand { get; }
    public IRelayCommand ReturnProgramMonitorTraceToRealtimeCommand { get; }
    public IAsyncRelayCommand ExportProgramMonitorTraceCsvCommand { get; }
    public IAsyncRelayCommand SaveProgramMonitorTraceSessionCommand { get; }
    public IAsyncRelayCommand LoadProgramMonitorTraceSessionCommand { get; }
    public IRelayCommand LocateProgramMonitorTraceTimeCommand { get; }
    public IRelayCommand JumpToAlarmPageCommand { get; }
    public IRelayCommand JumpToAuditPageCommand { get; }
    public IAsyncRelayCommand ExportFlowIssueReportCommand { get; }

    public string CurrentSubSection => Shell.CurrentMonitorSubSection;
    public string CurrentMonitorTitle => Shell.CurrentMonitorTitle;
    public ObservableCollection<TagItem> Tags => Shell.Tags;
    public ObservableCollection<OpcUaBrowseNode> OpcUaBrowserNodes => Shell.OpcUaBrowserNodes;
    public ObservableCollection<FlowStepRecord> FlowSteps => Shell.FlowSteps;
    public ObservableCollection<TrendSample> TrendSamples => Shell.TrendSamples;
    public ObservableCollection<string> MonitorCategoryOptions => Shell.MonitorCategoryOptions;
    public ObservableCollection<string> FlowFilterOptions => Shell.FlowFilterOptions;
    public ObservableCollection<string> FlowTimeRangeOptions => Shell.FlowTimeRangeOptions;
    public ObservableCollection<string> FlowStepFilterOptions => Shell.FlowStepFilterOptions;
    public ObservableCollection<string> ProgramMonitorTraceFlowOptions => Shell.ProgramMonitorTraceFlowOptions;
    public ObservableCollection<FlowIssueSummary> FlowIssueSummaries => Shell.FlowIssueSummaries;
    public ObservableCollection<IoMonitorItem> IoMonitorLeftItems => Shell.IoMonitorLeftItems;
    public ObservableCollection<IoMonitorItem> IoMonitorRightItems => Shell.IoMonitorRightItems;
    public ICollectionView FlowStepsView => Shell.FlowStepsView;
    public string CommunicationStatus => Shell.CommunicationStatus;
    public string OpcUaBrowserStatus => Shell.OpcUaBrowserStatus;
    public OpcUaBrowseNode? SelectedOpcUaBrowseNode { get => Shell.SelectedOpcUaBrowseNode; set => Shell.SelectedOpcUaBrowseNode = value; }
    public string SelectedOpcUaNodeValue => Shell.SelectedOpcUaNodeValue;
    public string SelectedOpcUaNodeStatus => Shell.SelectedOpcUaNodeStatus;
    public string SelectedOpcUaNodeTimestamp => Shell.SelectedOpcUaNodeTimestamp;
    public bool IsMonitorProductionPageVisible => Shell.IsMonitorProductionPageVisible;
    public bool IsMonitorSinglePanelPageVisible => Shell.IsMonitorSinglePanelPageVisible;
    public bool IsMonitorIoPageVisible => Shell.IsMonitorIoPageVisible;
    public bool IsMonitorProgramPageVisible => Shell.IsMonitorProgramPageVisible;
    public bool IsMonitorCommunicationPageVisible => Shell.IsMonitorCommunicationPageVisible;
    public int ShiftProductionCount => Shell.ShiftProductionCount;
    public int GoodCount => Shell.GoodCount;
    public int NgCount => Shell.NgCount;
    public int TargetCount => Shell.TargetCount;
    public double AvailabilityRate => Shell.AvailabilityRate;
    public double PerformanceRate => Shell.PerformanceRate;
    public double QualityRate => Shell.QualityRate;
    public string ShiftStatusText => Shell.ShiftStatusText;
    public string CurrentRecipeText => Shell.CurrentRecipeText;
    public string CurrentOrderText => Shell.CurrentOrderText;
    public string ProductionTrendSummary => Shell.ProductionTrendSummary;
    public string OeeTrendSummary => Shell.OeeTrendSummary;
    public string AlarmTrendSummary => Shell.AlarmTrendSummary;
    public string TimeAxisSummary => Shell.TimeAxisSummary;
    public string ProductionTrendPath => Shell.ProductionTrendPath;
    public string OeeTrendPath => Shell.OeeTrendPath;
    public string AlarmTrendPath => Shell.AlarmTrendPath;
    public string FlowStepTrendPath => Shell.FlowStepTrendPath;
    public string FlowIssueTrendPath => Shell.FlowIssueTrendPath;
    public string FlowRankingSummary => Shell.FlowRankingSummary;
    public string SelectedFlowSummary => Shell.SelectedFlowSummary;
    public string IoMonitorTitle => Shell.IoMonitorTitle;
    public string IoMonitorDiButtonBackground => Shell.IoMonitorDiButtonBackground;
    public string IoMonitorDiButtonForeground => Shell.IoMonitorDiButtonForeground;
    public string IoMonitorDoButtonBackground => Shell.IoMonitorDoButtonBackground;
    public string IoMonitorDoButtonForeground => Shell.IoMonitorDoButtonForeground;
    public string ProgramMonitorMainFlowTracePath => Shell.ProgramMonitorMainFlowTracePath;
    public string ProgramMonitorSubFlow2TracePath => Shell.ProgramMonitorSubFlow2TracePath;
    public string ProgramMonitorSubFlow3TracePath => Shell.ProgramMonitorSubFlow3TracePath;
    public string ProgramMonitorTraceAxisTopLabel => Shell.ProgramMonitorTraceAxisTopLabel;
    public string ProgramMonitorTraceAxisMidHighLabel => Shell.ProgramMonitorTraceAxisMidHighLabel;
    public string ProgramMonitorTraceAxisMidLowLabel => Shell.ProgramMonitorTraceAxisMidLowLabel;
    public string ProgramMonitorTraceAxisBottomLabel => Shell.ProgramMonitorTraceAxisBottomLabel;
    public string ProgramMonitorTraceStartLabel => Shell.ProgramMonitorTraceStartLabel;
    public string ProgramMonitorTraceEndLabel => Shell.ProgramMonitorTraceEndLabel;
    public string ProgramMonitorTraceLatestText => Shell.ProgramMonitorTraceLatestText;
    public string ProgramMonitorTraceWindowText => Shell.ProgramMonitorTraceWindowText;
    public bool HasProgramMonitorTraceHistory => Shell.HasProgramMonitorTraceHistory;
    public string ProgramMonitorTraceReplayText => Shell.ProgramMonitorTraceReplayText;
    public int ProgramMonitorTraceReplayMaximum => Shell.ProgramMonitorTraceReplayMaximum;
    public string ProgramMonitorCursorText => Shell.ProgramMonitorCursorText;
    public string ProgramMonitorCursorAText => Shell.ProgramMonitorCursorAText;
    public string ProgramMonitorCursorBText => Shell.ProgramMonitorCursorBText;
    public string ProgramMonitorCursorDeltaText => Shell.ProgramMonitorCursorDeltaText;

    public string SelectedMonitorCategory { get => Shell.SelectedMonitorCategory; set => Shell.SelectedMonitorCategory = value; }
    public string SelectedFlowFilter { get => Shell.SelectedFlowFilter; set => Shell.SelectedFlowFilter = value; }
    public string SelectedFlowTimeRange { get => Shell.SelectedFlowTimeRange; set => Shell.SelectedFlowTimeRange = value; }
    public string SelectedFlowStepFilter { get => Shell.SelectedFlowStepFilter; set => Shell.SelectedFlowStepFilter = value; }
    public bool ShowOnlyAbnormalFlow { get => Shell.ShowOnlyAbnormalFlow; set => Shell.ShowOnlyAbnormalFlow = value; }
    public double ProgramMonitorTraceWindowMinutes { get => Shell.ProgramMonitorTraceWindowMinutes; set => Shell.ProgramMonitorTraceWindowMinutes = value; }
    public bool ProgramMonitorTraceShowLine1 { get => Shell.ProgramMonitorTraceShowLine1; set => Shell.ProgramMonitorTraceShowLine1 = value; }
    public bool ProgramMonitorTraceShowLine2 { get => Shell.ProgramMonitorTraceShowLine2; set => Shell.ProgramMonitorTraceShowLine2 = value; }
    public bool ProgramMonitorTraceShowLine3 { get => Shell.ProgramMonitorTraceShowLine3; set => Shell.ProgramMonitorTraceShowLine3 = value; }
    public string SelectedProgramMonitorTraceFlow { get => Shell.SelectedProgramMonitorTraceFlow; set => Shell.SelectedProgramMonitorTraceFlow = value; }
    public int ProgramMonitorTraceReplayPosition { get => Shell.ProgramMonitorTraceReplayPosition; set => Shell.ProgramMonitorTraceReplayPosition = value; }
    public string ProgramMonitorTraceLocateTime { get => Shell.ProgramMonitorTraceLocateTime; set => Shell.ProgramMonitorTraceLocateTime = value; }
}
