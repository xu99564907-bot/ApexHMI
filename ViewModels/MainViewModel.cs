using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using ApexHMI.Interfaces;
using ApexHMI.Models;
using ApexHMI.Models.Sfc;
using ApexHMI.Services;
using ApexHMI.Services.Security;
using Serilog;

namespace ApexHMI.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private static readonly Regex OperationNumberPattern = new(@"\bOP\s*0*(\d{1,3})\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex DbNumberPattern = new(@"\bDB\s*0*(\d{3,5})\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex CylinderRootPattern = new(@"\.CylCtrl\[(\d+)\]", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly IOpcUaService _opcUaService;
    private readonly CsvImportService _csvImportService;
    private readonly XmlImportService _xmlImportService;
    private readonly IoTableImportService _ioTableImportService;
    private readonly IoProgramGenerationService _ioProgramGenerationService;
    private readonly IConfigurationService _configurationService;
    private readonly NamingRulesService _namingRulesService;
    private readonly IParameterService _parameterService;
    private readonly IAlarmService _alarmService;
    private readonly FlowLogCsvService _flowLogCsvService;
    private readonly IRecipeService _recipeService;
    private readonly TrendHistoryService _trendHistoryService;
    private readonly GitPullService _gitPullService;
    private readonly GeneratedArtifactSyncService _generatedArtifactSyncService;
    private readonly IUserService _userService;
    private readonly DispatcherTimer _subscriptionTimer;
    private readonly DispatcherTimer _opcUaBrowserRefreshTimer;
    private readonly object _ioTableRowsSync = new();
    private readonly object _generatedIoProgramsSync = new();
    private readonly object _manualCylinderBlocksSync = new();
    private readonly object _logsSync = new();
    private int _controlDbMultiplier = 100;
    private int _controlDbOffset;
    private int _driveDbOffset = 50;
    private List<AxisConfigEntry> _axisConfigEntries = new();
    private readonly Dictionary<string, AlarmRecord> _activeAlarmMap = new(StringComparer.OrdinalIgnoreCase);
    private NamingRulesConfig _namingRules = NamingRulesConfig.CreateDefault();
    private string _currentIoSourceFilePath = string.Empty;
    private int _currentIoSourceEncodingCodePage = 65001;
    private List<string> _currentIoSourceHeaders = new();
    private readonly List<IoTableRow> _importedIoRowsSnapshot = new();
    private readonly List<IoTableRow> _lastIoSavedSnapshot = new();
    private string _lastIoSavedFilePath = string.Empty;
    private string _lastIoHistoryFilePath = string.Empty;
    private DateTime _lastIoSaveAt = DateTime.MinValue;
    private DesignerElement? _clipboardElement;
    private DesignerElement? _designerSelectionSubscription;
    private bool _isRefreshing;
    private FlowStepRecord? _programMonitorCursorASample;
    private FlowStepRecord? _programMonitorCursorBSample;
    private readonly List<FlowStepRecord> _programMonitorTraceHistory = new();
    private bool _programMonitorTraceFollowNow = true;
    private DateTime _programMonitorTraceWindowEnd = DateTime.Now;
    private bool _isRefreshingSelectedOpcUaNode;
    private bool _isLoadingSelectedCylinderParmSettings;

    // 绑定点(变量名 / Application. 路径 / ns=… 路径) 的最近一次 OPC 读。用于：变量表未建 Tag 时、FindTag 未命中时，GetTagValue/指示灯仍能显示与调试器一致的读数。
    private readonly Dictionary<string, string> _opcStringValueByBindingKey = new(StringComparer.OrdinalIgnoreCase);

    public event Action<string, string, string>? PopupRequested;
    public event Func<string, string, bool>? ConfirmationRequested;
    public event Action<string, string?>? SectionJumpRequested;
    public event Action<string, string?>? HighlightRequested;

    public ObservableCollection<NavigationItemViewModel> NavigationItems { get; } = new();
    public ObservableCollection<TagItem> Tags { get; } = new();
    public ObservableCollection<EventBinding> EventBindings { get; } = new();
    public ObservableCollection<AlarmRecord> CurrentAlarms { get; } = new();
    public ObservableCollection<AlarmRecord> AlarmHistory { get; } = new();
    public ObservableCollection<AlarmRecord> Logs { get; } = new();
    public ObservableCollection<DesignerElement> DesignerElements { get; } = new();
    public ObservableCollection<DesignerPage> DesignerPages { get; } = new();
    public ObservableCollection<string> ToolboxItems { get; } = new() { "Button", "Indicator", "Label", "ValueDisplay", "AlarmBanner", "Motor", "Cylinder", "Axis", "Robot", "Stopper", "PageButton" };
    public ObservableCollection<string> RuntimeTemplates { get; } = new() { "主界面", "监控画面", "手动画面", "参数设定", "报警画面" };
    public ObservableCollection<string> CommunicationProtocolOptions { get; } = new() { "OPC UA", "Modbus TCP", "MC Protocol" };
    public ObservableCollection<string> DesignerActionOptions { get; } = new() { "", "变量翻转", "置位", "复位", "脉冲", "页面跳转", "气缸回原", "气缸动点" };
    public ObservableCollection<string> DesignerNavigationTargetOptions { get; } = new() { "主界面", "监控", "手动操作", "参数设定", "配方管理", "报警画面", "登录", "设计器" };
    public ObservableCollection<ParameterItem> Parameters { get; } = new();
    public ObservableCollection<UserRole> Roles { get; } = new() { UserRole.Operator, UserRole.Engineer, UserRole.Administrator };
    public ObservableCollection<string> MonitorCategoryOptions { get; } = new() { "全部", "Production", "Alarm", "Axis", "Motor", "Cylinder", "Robot", "IO" };
    public ObservableCollection<string> AlarmLevelOptions { get; } = new() { "全部", "Alarm", "Error", "Warning", "Info" };
    public ObservableCollection<string> AlarmTimeRangeOptions { get; } = new() { "全部", "本班次", "今日", "近7天" };
    public ObservableCollection<AlarmRecord> AlarmStatistics { get; } = new();
    public ObservableCollection<OperationAuditRecord> OperationAudits { get; } = new();
    public ObservableCollection<FlowStepRecord> FlowSteps { get; } = new();
    public ObservableCollection<FlowIssueSummary> FlowIssueSummaries { get; } = new();
    public ObservableCollection<RecipeItem> Recipes { get; } = new();
    public ObservableCollection<ParameterItem> ActiveRecipeParameters { get; } = new();
    public ObservableCollection<TrendSample> TrendSamples { get; } = new();
    public ObservableCollection<OpcUaBrowseNode> OpcUaBrowserNodes { get; } = new();

    // M17 OPC UA 节点搜索 (高亮所有 DisplayName / NodeId 包含搜索词的节点)
    [ObservableProperty] private string opcUaBrowserSearchText = string.Empty;

    // M19 OPC UA 节点订阅 quality 变化日志 (短期内存保留，用于诊断断连)
    public ObservableCollection<string> OpcUaQualityLog { get; } = new();
    public ObservableCollection<IoTableRow> IoTableRows { get; } = new();
    public ObservableCollection<ManualCylinderBlockItem> ManualCylinderBlocks { get; } = new();
    public ObservableCollection<ManualAxisBlockItem> ManualAxisBlocks { get; } = new();
    public ObservableCollection<GeneratedProgramArtifact> GeneratedIoPrograms { get; } = new();
    public ObservableCollection<SfcStep> SfcSteps { get; } = new();
    public ObservableCollection<SfcStep> SfcInitSteps { get; } = new();
    public ObservableCollection<string> FlowFilterOptions { get; } = new() { "全部", "主线1", "主线2", "主线3" };
    // M5: 加 "自定义" 让用户用 DatePicker 指定起止时间
    public ObservableCollection<string> FlowTimeRangeOptions { get; } = new() { "全部", "本班次", "今日", "近7天", "自定义" };
    public ObservableCollection<string> FlowStepFilterOptions { get; } = new() { "全部", "10", "20", "30", "40", "50", "60" };
    public ObservableCollection<string> IoPlcTemplateOptions { get; } = new() { "汇川中型PLC", "汇川小型PLC", "西门子PLC" };
    public ObservableCollection<string> ProgramMonitorTraceFlowOptions { get; } = new() { "主线1", "主线2", "主线3" };

    [ObservableProperty] private OpcUaConnectionOptions connection = new();
    [ObservableProperty] private int selectedTabIndex;
    [ObservableProperty] private string currentMonitorSubSection = "输入输出监控";
[ObservableProperty] private string currentManualSubSection = "气缸";
    [ObservableProperty] private string currentParameterSubSection = "系统参数设定";
    [ObservableProperty] private string currentAlarmSubSection = "当前报警";
[ObservableProperty] private string currentDesignerSubSection = "手动程序生成";
    [ObservableProperty] private string currentSection = "主界面";
    [ObservableProperty] private string systemMessage = "系统就绪";
    [ObservableProperty] private string loginUser = "操作员";
    // H6 / H7 主界面流程日志过滤 + 搜索（FlowStepsView Filter 应用）
    [ObservableProperty] private bool flowLogShowInfo = true;
    [ObservableProperty] private bool flowLogShowWarn = true;
    [ObservableProperty] private bool flowLogShowError = true;
    [ObservableProperty] private string flowLogSearchText = string.Empty;

    // M5 监控页自定义时间范围（仅 SelectedFlowTimeRange == "自定义" 时显示 DatePicker）
    [ObservableProperty] private DateTime customTimeFrom = DateTime.Today.AddDays(-1);
    [ObservableProperty] private DateTime customTimeTo = DateTime.Today.AddDays(1);

    // M8 监控页"流程/OEE/报警趋势"卡的多线显示开关
    [ObservableProperty] private bool showOeeTrendLine = true;
    [ObservableProperty] private bool showAlarmTrendLine = true;
    [ObservableProperty] private bool showFlowStepTrendLine = true;
    [ObservableProperty] private bool showFlowIssueTrendLine = false;

    // M6 监控页 工单/配方 切换提示（最近一次更新时间）
    [ObservableProperty] private DateTime? lastOrderChangeAt;
    [ObservableProperty] private DateTime? lastRecipeChangeAt;
    /// <summary>UI 全局缩放因子，应用到主窗口 LayoutTransform。范围 0.7 ~ 1.5。</summary>
    [ObservableProperty] private double uiScale = 1.0;
    [ObservableProperty] private string manualWriteTagName = string.Empty;
    [ObservableProperty] private string manualWriteValue = string.Empty;
    [ObservableProperty] private string newTagName = string.Empty;
    [ObservableProperty] private string newTagNodeId = string.Empty;
    [ObservableProperty] private string newTagDataType = "Boolean";
    [ObservableProperty] private string newTagCategory = "General";
    [ObservableProperty] private string newTagGroup = "Default";
    [ObservableProperty] private string newTagDirection = "Input";
    [ObservableProperty] private bool newTagIsAlarm;
    [ObservableProperty] private bool newTagIsWritable = true;
    [ObservableProperty] private string newEventTagName = string.Empty;
    [ObservableProperty] private string newEventTriggerCondition = "ValueChanged";
    [ObservableProperty] private string newEventName = string.Empty;
    [ObservableProperty] private string newEventTarget = string.Empty;
    [ObservableProperty] private string newEventParameter = string.Empty;
    [ObservableProperty] private DesignerElement? selectedDesignerElement;
    [ObservableProperty] private string selectedToolboxItem = "Button";
    [ObservableProperty] private double designerCanvasWidth = 1280;
    [ObservableProperty] private double designerCanvasHeight = 720;
    [ObservableProperty] private string designerPageName = "主界面";
    [ObservableProperty] private string designerProjectName = "PLC HMI Project";
    [ObservableProperty] private DesignerPage? selectedDesignerPage;
    [ObservableProperty] private bool isRuntimeMode = true;
    [ObservableProperty] private string dragToolboxItem = string.Empty;
    [ObservableProperty] private bool enableGridSnap = true;
    [ObservableProperty] private int gridSize = 10;
    [ObservableProperty] private bool autoRefreshEnabled = true;
    [ObservableProperty] private int refreshIntervalMs = 600;
    [ObservableProperty] private bool useOpcSubscription = true;
    [ObservableProperty] private string selectedRuntimeTemplate = "主界面";
    [ObservableProperty] private UserRole currentUserRole = UserRole.Operator;
    [ObservableProperty] private string loginPassword = string.Empty;
    [ObservableProperty] private string axisJogDistance = "10";
    [ObservableProperty] private string axisTargetPosition = "100";
    [ObservableProperty] private bool cylinderHomeMaskEnabled;
    [ObservableProperty] private bool cylinderWorkMaskEnabled;
    [ObservableProperty] private string cylinderConfiguredName = string.Empty;
    [ObservableProperty] private string cylinderHomeCommandTagName = string.Empty;
    [ObservableProperty] private string cylinderWorkCommandTagName = string.Empty;
    [ObservableProperty] private string cylinderHomeSensorTagName = string.Empty;
    [ObservableProperty] private string cylinderWorkSensorTagName = string.Empty;
    [ObservableProperty] private string cylinderHomeInterlockTagName = string.Empty;
    [ObservableProperty] private string cylinderWorkInterlockTagName = string.Empty;
    [ObservableProperty] private ManualCylinderBlockItem? selectedCylinderSettingsBlock;
    [ObservableProperty] private ManualAxisBlockItem? selectedAxisSettingsBlock;
    [ObservableProperty] private RobotControlViewModel? robotControlViewModel;
    [ObservableProperty] private string cylinderAlarmTimeSetting = "20";
    [ObservableProperty] private string cylinderHomeDelaySetting = "3";
    [ObservableProperty] private string cylinderWorkDelaySetting = "3";
    [ObservableProperty] private string cylinderCurrentActionTimeDisplay = "--";
    [ObservableProperty] private string cylinderLastActionTimeDisplay = "--";
    [ObservableProperty] private int cylinderActionCount;
    [ObservableProperty] private string selectedMonitorCategory = "全部";
    [ObservableProperty] private string selectedAlarmLevel = "全部";
    [ObservableProperty] private string selectedAlarmTimeRange = "全部";
    [ObservableProperty] private bool showOnlyFocusAlarms;
    [ObservableProperty] private double startHoldProgress;
    [ObservableProperty] private int currentFlowStepNo;
    [ObservableProperty] private string currentFlowComment = "自动流程待命";
    [ObservableProperty] private string selectedRecipeName = "产品A";
    [ObservableProperty] private string selectedFlowFilter = "全部";
    [ObservableProperty] private string selectedFlowTimeRange = "全部";
    [ObservableProperty] private string selectedFlowStepFilter = "全部";
    [ObservableProperty] private bool showOnlyAbnormalFlow;
    [ObservableProperty] private double programMonitorTraceWindowMinutes = 5;
    [ObservableProperty] private bool programMonitorTraceShowLine1 = true;
    [ObservableProperty] private bool programMonitorTraceShowLine2 = true;
    [ObservableProperty] private bool programMonitorTraceShowLine3 = true;
    [ObservableProperty] private string selectedProgramMonitorTraceFlow = "主线1";
    [ObservableProperty] private bool programMonitorTracePaused;
    [ObservableProperty] private bool programMonitorTraceReplayMode;
    [ObservableProperty] private int programMonitorTraceReplayPosition;
    [ObservableProperty] private int programMonitorTraceReplayMaximum;
    [ObservableProperty] private string programMonitorTraceReplayText = "模式：实时采样";
    [ObservableProperty] private string programMonitorTraceLocateTime = string.Empty;
    [ObservableProperty] private string programMonitorCursorText = "光标：--";
    [ObservableProperty] private string programMonitorCursorAText = "光标A：--";
    [ObservableProperty] private string programMonitorCursorBText = "光标B：--";
    [ObservableProperty] private string programMonitorCursorDeltaText = "Δ：--";
    [ObservableProperty] private string jumpAlarmKeyword = string.Empty;
    [ObservableProperty] private string selectedIoPlcTemplate = "汇川中型PLC";
    [ObservableProperty] private string ioOperationNumber = "OP10";
    [ObservableProperty] private int ioSaveIntervalMinutes = 5;
    [ObservableProperty] private string ioImportSummary = "尚未导入 IO 表";
    [ObservableProperty] private string generatedIoOutputDirectory = string.Empty;
    [ObservableProperty] private GeneratedProgramArtifact? selectedGeneratedIoProgram;
    [ObservableProperty] private SfcStep? selectedSfcStep;
    [ObservableProperty] private string sfcGeneratedCode = "配置步骤后点击「生成代码」。";
    [ObservableProperty] private string sfcProgramName = "主装配流程";
    [ObservableProperty] private string sfcStationNo = "1";
    [ObservableProperty] private SfcStep? selectedSfcInitStep;
    [ObservableProperty] private string sfcInitGeneratedCode = "配置步骤后点击「生成初始化程序」。";
    [ObservableProperty] private string sfcInitProgramName = "初始化流程";
    [ObservableProperty] private string sfcInitStationNo = "1";

    private SfcStep? _prevSfcStep;
    /// <summary>各工位 SFC 配置缓存，key = 工位号字符串（如 "1"）</summary>
    internal readonly Dictionary<string, SfcProgramConfig> _sfcProgramsByStation = new();
    /// <summary>上一个生效的工位号，用于切站前保存</summary>
    private string _prevSfcStationNo = "1";
    /// <summary>初始化阶段禁止切站钩子触发，避免递归</summary>
    private bool _suppressStationSwitch;

    [ObservableProperty] private OpcUaBrowseNode? selectedOpcUaBrowseNode;
    [ObservableProperty] private string selectedOpcUaNodeValue = "--";
    [ObservableProperty] private string selectedOpcUaNodeStatus = "未读取";
    [ObservableProperty] private string selectedOpcUaNodeTimestamp = "--";
    [ObservableProperty] private string opcUaBrowserStatus = "连接 OPC UA 后，可在这里浏览服务器节点。";

    public ICollectionView ManualCylinderBlocksView { get; }

    public IEnumerable<ManualAxisBlockItem> ManualAxisBlockCards
    {
        get
        {
            var distinct = ManualAxisBlocks
                .GroupBy(item => item.AxisIndex)
                .Select(group => group.OrderBy(item => item.DisplayOrder).First())
                .OrderBy(item => item.DisplayOrder)
                .ThenBy(item => item.AxisIndex);

            // MA9 轴搜索（按 DisplayName 模糊过滤）
            var keyword = ManualAxisSearchText?.Trim();
            if (!string.IsNullOrEmpty(keyword))
            {
                distinct = distinct.Where(item =>
                    (item.DisplayName?.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0)
                    .OrderBy(item => item.DisplayOrder)
                    .ThenBy(item => item.AxisIndex);
            }
            return distinct;
        }
    }

    // MA9 轴搜索文本
    [ObservableProperty] private string manualAxisSearchText = string.Empty;
    partial void OnManualAxisSearchTextChanged(string value) => OnPropertyChanged(nameof(ManualAxisBlockCards));

    public IEnumerable<ManualCylinderBlockItem> ManualCylinderBlockCards
    {
        get
        {
            var distinct = ManualCylinderBlocks
                .GroupBy(item => item.CylinderIndex)
                .Select(group => group.OrderBy(item => item.DisplayOrder).First())
                .OrderBy(item => item.DisplayOrder)
                .ThenBy(item => item.CylinderIndex);

            // MA5 按 DisplayName / 命令地址 模糊过滤
            var keyword = ManualCylinderSearchText?.Trim();
            if (!string.IsNullOrEmpty(keyword))
            {
                distinct = distinct.Where(item =>
                    (item.DisplayName?.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0
                    || (item.WorkCommandAddress?.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0
                    || (item.HomeCommandAddress?.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0
                    || (item.WorkSensorAddress?.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0
                    || (item.HomeSensorAddress?.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0)
                    .OrderBy(item => item.DisplayOrder)
                    .ThenBy(item => item.CylinderIndex);
            }
            return distinct;
        }
    }

    // MA5 气缸搜索文本（按 DisplayName / 命令 / 传感器地址过滤）
    [ObservableProperty] private string manualCylinderSearchText = string.Empty;

    partial void OnManualCylinderSearchTextChanged(string value) => OnPropertyChanged(nameof(ManualCylinderBlockCards));

    public ICollectionView MonitorTagsView => CollectionViewSource.GetDefaultView(Tags);
    public ICollectionView ParametersView => CollectionViewSource.GetDefaultView(Parameters);
    public ICollectionView AlarmStatisticsView => CollectionViewSource.GetDefaultView(AlarmStatistics);
    public ICollectionView FlowStepsView => CollectionViewSource.GetDefaultView(FlowSteps);
    public string CommunicationStatus => _opcUaService.ConnectionStatus;
    public int TagCount => Tags.Count;
    public int AlarmCount => CurrentAlarms.Count(a => a.Active);
    public string DesignerModeText => IsRuntimeMode ? "运行态" : "设计态";
    public bool HasClipboard => _clipboardElement is not null;
    public bool CanEditParameters => CurrentUserRole >= UserRole.Engineer;
    public bool CanAdmin => CurrentUserRole == UserRole.Administrator;
    public bool CanOperateDevices => CurrentUserRole >= UserRole.Operator;
    public bool IsDesignMode => !IsRuntimeMode;
    public bool IsRuntimeDashboardVisible => IsRuntimeMode;
    public string RuntimeHeaderText => IsRuntimeMode ? "正式运行页" : "当前处于设计态";
    public string CurrentRoleText => CurrentUserRole switch
    {
        UserRole.Operator => "操作员",
        UserRole.Engineer => "工程师",
        UserRole.Administrator => "管理员",
        _ => "未知"
    };
    public int ActiveAlarmCount => CurrentAlarms.Count(a => a.Active);
    public int UnacknowledgedAlarmCount => CurrentAlarms.Count(a => a.Active && !a.Acknowledged);
    public int ProductionCount => GetIntTag("Production_Count", 1280);
    public int GoodCount => GetIntTag("Production_GoodCount", 1246);
    public int NgCount => GetIntTag("Production_NgCount", 34);
    public int ShiftProductionCount => GetIntTag("Shift_ProductionCount", 460);
    public int ShiftGoodCount => GetIntTag("Shift_GoodCount", 450);
    public int ShiftNgCount => GetIntTag("Shift_NgCount", 10);
    public int DailyProductionCount => GetIntTag("Daily_ProductionCount", 1280);
    public int DailyGoodCount => GetIntTag("Daily_GoodCount", 1246);
    public int DailyNgCount => GetIntTag("Daily_NgCount", 34);
    public int TargetCount => GetIntTag("Production_TargetCount", 1500);
    public int HourlyThroughput => GetIntTag("Throughput_Hourly", 380);
    public double CycleTimeSeconds => GetDoubleTag("Cycle_Time", 3.2);
    public int MachineRunTimeMin => GetIntTag("Machine_RunTimeMin", 420);
    public int MachineStopTimeMin => GetIntTag("Machine_StopTimeMin", 34);
    public double AvailabilityRate => CalculateAvailability();
    public double PerformanceRate => CalculatePerformance();
    public double QualityRate => CalculateQuality();
    public double OeeRate => Math.Round(AvailabilityRate * PerformanceRate * QualityRate / 10000.0, 1);
    public string DeviceStatusText => ActiveAlarmCount > 0 ? "报警中" : GetBoolTag("Device_Start") ? "运行中" : "待机";
    public string ShiftStatusText => ShiftProductionCount >= TargetCount ? "班次达成" : "班次生产中";
    public string CurrentRecipeText => string.IsNullOrWhiteSpace(SelectedRecipeName) ? (GetTagValue("Recipe_Name") == "--" ? "产品A" : GetTagValue("Recipe_Name")) : SelectedRecipeName;
    public string CurrentOrderText => GetTagValue("WorkOrder_No") == "--" ? "WO-20260404-01" : GetTagValue("WorkOrder_No");
    public string MotorStatusText => GetBoolTag("Motor1_Fault") ? "故障" : GetBoolTag("Y_RunLamp") ? "运行" : "停止";
    public string CylinderStatusText => CylinderForwardActive ? ResolveCurrentCylinderWorkPositionLabel() : CylinderBackwardActive ? ResolveCurrentCylinderHomePositionLabel() : "切换中";
    public string CylinderDisplayName => string.IsNullOrWhiteSpace(CylinderConfiguredName) ? GetImportedCylinderDisplayName() : CylinderConfiguredName;
    public string CylinderHomeMaskButtonText => CylinderHomeMaskEnabled ? "原点屏蔽：开" : "原点屏蔽：关";
    public string CylinderWorkMaskButtonText => CylinderWorkMaskEnabled ? "动点屏蔽：开" : "动点屏蔽：关";
    public string SelectedCylinderHomeCommandBinding => SelectedCylinderSettingsBlock?.HomeCommandTagName ?? CylinderHomeCommandTagName;
    public string SelectedCylinderWorkCommandBinding => SelectedCylinderSettingsBlock?.WorkCommandTagName ?? CylinderWorkCommandTagName;
    public string SelectedCylinderHomeSensorBinding => SelectedCylinderSettingsBlock?.HomeSensorTagName ?? CylinderHomeSensorTagName;
    public string SelectedCylinderWorkSensorBinding => SelectedCylinderSettingsBlock?.WorkSensorTagName ?? CylinderWorkSensorTagName;
    public string SelectedCylinderHomeInterlockBinding => SelectedCylinderSettingsBlock?.HomeInterlockTagName ?? CylinderHomeInterlockTagName;
    public string SelectedCylinderWorkInterlockBinding => SelectedCylinderSettingsBlock?.WorkInterlockTagName ?? CylinderWorkInterlockTagName;
    public string SelectedCylinderHomeDisplayBinding => GetSelectedCylinderDisplayTagName("DevStatus.Valve_Home");
    public string SelectedCylinderWorkDisplayBinding => GetSelectedCylinderDisplayTagName("DevStatus.Valve_Work");
    public string SelectedCylinderHomeDisplayFallbackBinding => GetSelectedCylinderDisplayTagName("DevStatus.Valve_Home");
    public string SelectedCylinderWorkDisplayFallbackBinding => GetSelectedCylinderDisplayTagName("DevStatus.Valve_Work");
    public string SelectedCylinderDisableHomeBinding => GetSelectedCylinderParmTagName("DisableHome");
    public string SelectedCylinderDisableWorkBinding => GetSelectedCylinderParmTagName("DisableWork");
    public string SelectedCylinderErrorDelayBinding => GetSelectedCylinderParmTagName("Error_Delay");
    public string SelectedCylinderHomeDelayBinding => GetSelectedCylinderParmTagName("Home_Delay");
    public string SelectedCylinderWorkDelayBinding => GetSelectedCylinderParmTagName("Work_Delay");
    public string SelectedCylinderHomeCommandValue => GetTagValue(SelectedCylinderHomeCommandBinding);
    public string SelectedCylinderWorkCommandValue => GetTagValue(SelectedCylinderWorkCommandBinding);
    public string SelectedCylinderHomeSensorValue => GetTagValue(SelectedCylinderHomeSensorBinding);
    public string SelectedCylinderWorkSensorValue => GetTagValue(SelectedCylinderWorkSensorBinding);
    public string SelectedCylinderHomeInterlockValue => GetTagValue(SelectedCylinderHomeInterlockBinding);
    public string SelectedCylinderWorkInterlockValue => GetTagValue(SelectedCylinderWorkInterlockBinding);
    public string SelectedCylinderHomeDisplayValue => GetTagValue(SelectedCylinderHomeDisplayBinding);
    public string SelectedCylinderWorkDisplayValue => GetTagValue(SelectedCylinderWorkDisplayBinding);
    public string SelectedCylinderHomeDisplayFallbackValue => GetTagValue(SelectedCylinderHomeDisplayFallbackBinding);
    public string SelectedCylinderWorkDisplayFallbackValue => GetTagValue(SelectedCylinderWorkDisplayFallbackBinding);
    public string SelectedAxisPowerCommandBinding => SelectedAxisSettingsBlock?.PowerCommandTagName ?? string.Empty;
    public string SelectedAxisStopCommandBinding => SelectedAxisSettingsBlock?.StopCommandTagName ?? string.Empty;
    public string SelectedAxisHomeCommandBinding => SelectedAxisSettingsBlock?.ManuToHomeTagName ?? string.Empty;
    public string SelectedAxisJogForwardBinding => SelectedAxisSettingsBlock?.ManuJogForwardTagName ?? string.Empty;
    public string SelectedAxisJogBackwardBinding => SelectedAxisSettingsBlock?.ManuJogBackwardTagName ?? string.Empty;
    public string SelectedAxisStartPositionCommandBinding => SelectedAxisSettingsBlock?.ManuPositionTagName ?? string.Empty;
    public string SelectedAxisReferenceCommandBinding => SelectedAxisSettingsBlock?.ManuToHomeTagName ?? string.Empty;
    public string SelectedAxisTeachEnableBinding => SelectedAxisSettingsBlock?.TeachOnTagName ?? string.Empty;
    public string SelectedAxisTeachWriteBinding => SelectedAxisSettingsBlock?.TeachTagName ?? string.Empty;
    public string SelectedAxisPointSelectBinding => SelectedAxisSettingsBlock?.PointSelectTagName ?? string.Empty;
    public string SelectedAxisMoveToPointBinding => SelectedAxisSettingsBlock?.ManuPointTagName ?? string.Empty;
    public string SelectedAxisSetPositionBinding => SelectedAxisSettingsBlock?.SetPositionTagName ?? string.Empty;
    public string SelectedAxisSetVelocityBinding => SelectedAxisSettingsBlock?.SetVelocityTagName ?? string.Empty;
    public string SelectedAxisHomeSignalBinding => SelectedAxisSettingsBlock?.HomeSignalTagName ?? string.Empty;
    public string SelectedAxisPositiveLimitBinding => SelectedAxisSettingsBlock?.PositiveLimitTagName ?? string.Empty;
    public string SelectedAxisNegativeLimitBinding => SelectedAxisSettingsBlock?.NegativeLimitTagName ?? string.Empty;
    public string SelectedAxisServoFeedbackBinding => SelectedAxisSettingsBlock?.ServoEnableFbTagName ?? string.Empty;
    public string SelectedAxisPowerOnBinding => SelectedAxisSettingsBlock?.PowerOnTagName ?? string.Empty;
    public string SelectedAxisBusyBinding => SelectedAxisSettingsBlock?.BusyTagName ?? string.Empty;
    public string SelectedAxisPosOkBinding => SelectedAxisSettingsBlock?.PosOkTagName ?? string.Empty;
    public string SelectedAxisInitializedBinding => SelectedAxisSettingsBlock?.InitializedTagName ?? string.Empty;
    public string SelectedAxisHomeInterlockBinding => SelectedAxisSettingsBlock?.HomeInterlockTagName ?? string.Empty;
    public string SelectedAxisJogInterlockBinding => SelectedAxisSettingsBlock?.JogInterlockTagName ?? string.Empty;
    public string SelectedAxisPositionInterlockBinding => SelectedAxisSettingsBlock?.PositioningInterlockTagName ?? string.Empty;
    public string SelectedAxisErrorBinding => SelectedAxisSettingsBlock?.ErrorTagName ?? string.Empty;
    public string SelectedAxisErrorIdBinding => SelectedAxisSettingsBlock?.ErrorIdTagName ?? string.Empty;
    public string SelectedAxisActualPositionBinding => SelectedAxisSettingsBlock?.ActualPositionTagName ?? string.Empty;
    public string SelectedAxisActualVelocityBinding => SelectedAxisSettingsBlock?.ActualVelocityTagName ?? string.Empty;
    public string SelectedAxisActualTorqueBinding => SelectedAxisSettingsBlock?.ActualTorqueTagName ?? string.Empty;
    public string SelectedAxisStateBinding => SelectedAxisSettingsBlock?.StateTagName ?? string.Empty;
    public string SelectedAxisPowerCommandValue => GetTagValue(SelectedAxisPowerCommandBinding);
    public string SelectedAxisStopCommandValue => GetTagValue(SelectedAxisStopCommandBinding);
    public string SelectedAxisHomeCommandValue => GetTagValue(SelectedAxisHomeCommandBinding);
    public string SelectedAxisJogForwardValue => GetTagValue(SelectedAxisJogForwardBinding);
    public string SelectedAxisJogBackwardValue => GetTagValue(SelectedAxisJogBackwardBinding);
    public string SelectedAxisStartPositionCommandValue => GetTagValue(SelectedAxisStartPositionCommandBinding);
    public string SelectedAxisReferenceCommandValue => GetTagValue(SelectedAxisReferenceCommandBinding);
    public string SelectedAxisTeachEnableValue => GetTagValue(SelectedAxisTeachEnableBinding);
    public string SelectedAxisTeachWriteValue => GetTagValue(SelectedAxisTeachWriteBinding);
    public string SelectedAxisPointSelectValue => GetTagValue(SelectedAxisPointSelectBinding);
    public string SelectedAxisMoveToPointValue => GetTagValue(SelectedAxisMoveToPointBinding);
    public string SelectedAxisSetPositionValue => GetTagValue(SelectedAxisSetPositionBinding);
    public string SelectedAxisSetVelocityValue => GetTagValue(SelectedAxisSetVelocityBinding);
    public string SelectedAxisHomeSignalValue => GetTagValue(SelectedAxisHomeSignalBinding);
    public string SelectedAxisPositiveLimitValue => GetTagValue(SelectedAxisPositiveLimitBinding);
    public string SelectedAxisNegativeLimitValue => GetTagValue(SelectedAxisNegativeLimitBinding);
    public string SelectedAxisServoFeedbackValue => GetTagValue(SelectedAxisServoFeedbackBinding);
    public string SelectedAxisPowerOnValue => GetTagValue(SelectedAxisPowerOnBinding);
    public string SelectedAxisBusyValue => GetTagValue(SelectedAxisBusyBinding);
    public string SelectedAxisPosOkValue => GetTagValue(SelectedAxisPosOkBinding);
    public string SelectedAxisInitializedValue => GetTagValue(SelectedAxisInitializedBinding);
    public string SelectedAxisHomeInterlockValue => GetTagValue(SelectedAxisHomeInterlockBinding);
    public string SelectedAxisJogInterlockValue => GetTagValue(SelectedAxisJogInterlockBinding);
    public string SelectedAxisPositionInterlockValue => GetTagValue(SelectedAxisPositionInterlockBinding);
    public string SelectedAxisErrorValue => GetTagValue(SelectedAxisErrorBinding);
    public string SelectedAxisErrorIdValue => GetTagValue(SelectedAxisErrorIdBinding);
    public string SelectedAxisActualPositionValue => GetTagValue(SelectedAxisActualPositionBinding);
    public string SelectedAxisActualVelocityValue => GetTagValue(SelectedAxisActualVelocityBinding);
    public string SelectedAxisActualTorqueValue => GetTagValue(SelectedAxisActualTorqueBinding);
    public string SelectedAxisStateValue => GetTagValue(SelectedAxisStateBinding);
    public string SelectedAxisCurrentStateText => SelectedAxisSettingsBlock?.CurrentStateText ?? "未选择轴";
    public string SelectedAxisInterlockHint => SelectedAxisSettingsBlock?.InterlockHint ?? string.Empty;
    public bool CylinderForwardActive => GetCylinderBool(CylinderWorkSensorTagName, ".Status.InWork", ".DevStatus.Sensor_Work", "Cylinder_FwdLS");
    public bool CylinderBackwardActive => GetCylinderBool(CylinderHomeSensorTagName, ".Status.InHome", ".DevStatus.Sensor_Home", "Cylinder_BwdLS");
    public bool CylinderOutputActive => GetCylinderBool(CylinderWorkCommandTagName, ".DevStatus.Valve_Work", ".Cmd.ManuToWork", "Cylinder_Extend");
    public string CylinderOutputText => CylinderOutputActive ? "伸出命令有效" : "回缩待命";
    public string CylinderCurrentStateText => ResolveCylinderCurrentStateText();
    public string CylinderActionHint => CylinderForwardActive ? "当前在前到位，适合执行回原或松开动作" : CylinderBackwardActive ? "当前在后到位，适合执行伸出或夹紧动作" : "气缸处于中间状态，等待动作完成后再切换";
    public bool CylinderInterlockBaseReady => !GetBoolTag("Alarm_EStop") && (!GetBoolTag("Y_RunLamp") || AllowManualCylinderWhenAuto) && (CylinderForwardActive || CylinderBackwardActive);
    public bool CylinderHomeInterlockActive => GetCylinderBool(CylinderHomeInterlockTagName, ".Parm.IC_Home", fallbackValue: CylinderInterlockBaseReady && CylinderForwardActive);
    public bool CylinderMoveInterlockActive => GetCylinderBool(CylinderWorkInterlockTagName, ".Parm.IC_Work", fallbackValue: CylinderInterlockBaseReady && CylinderBackwardActive);
    public string CylinderHomeInterlockText => CylinderHomeInterlockActive ? "允许回原" : GetCylinderBool(".Parm.IC_Home", fallbackValue: true) ? (CylinderBackwardActive ? "已在原点" : "回原互锁未满足") : "回原互锁未满足";
    public string CylinderMoveInterlockText => CylinderMoveInterlockActive ? "允许动点" : GetCylinderBool(".Parm.IC_Work", fallbackValue: true) ? (CylinderForwardActive ? "已在动点" : "动点互锁未满足") : "动点互锁未满足";
    public string CylinderInterlockHint => GetBoolTag("Alarm_EStop")
        ? "急停未复位，禁止操作气缸"
        : GetBoolTag("Y_RunLamp") && !AllowManualCylinderWhenAuto
            ? "设备自动运行中，当前联锁禁止手动气缸"
            : AllowManualCylinderWhenAuto
                ? "自动运行时允许手动切换气缸"
                : "当前允许手动操作气缸";
    public string AxisStatusText => GetBoolTag("Axis1_Alarm") ? "报警" : GetBoolTag("Axis1_Enable") ? $"使能 / 位置 {GetTagValue("Axis1_Pos")}" : "未使能";
    public string RobotStatusText => GetBoolTag("Robot_Pause") ? "暂停" : GetBoolTag("Robot_Run") ? "运行" : "待机";
    public bool IsDebugMode => GetBoolTag("Mode_Debug");
    public bool IsDryRunMode => GetBoolTag("Mode_DryRun");
    public bool IsBypassStationMode => GetBoolTag("Mode_BypassStation");
    public bool IsManualMode => GetBoolTag("Mode_Manual");
    public bool IsAutoMode => GetBoolTag("Mode_Auto");
    public string RunModeSummary => IsManualMode ? "人工模式" : IsAutoMode ? "自动模式" : "未选择";
    public string StartStopSummary => GetBoolTag("Device_Start") ? "设备已启动" : "设备已停止";
    public bool StartModeReady => IsManualMode || IsAutoMode;
    public bool StartAlarmReady => ActiveAlarmCount == 0;
    public bool StartInterlockReady => StartModeReady && StartAlarmReady;
    public string ProductionTrendSummary => $"班次 {ShiftProductionCount} / 日累计 {DailyProductionCount} / 目标 {TargetCount}";
    public string CurrentFlowStepText => $"STEP {CurrentFlowStepNo:000}";
    public string SelectedFlowSummary => SelectedFlowFilter == "全部" ? "多流程并行视图" : $"当前流程：{SelectedFlowFilter}";
    public string OeeTrendSummary => $"A {AvailabilityRate:F1}% / P {PerformanceRate:F1}% / Q {QualityRate:F1}% / OEE {OeeRate:F1}%";
    public string AlarmTrendSummary => $"活动 {ActiveAlarmCount} / 未确认 {UnacknowledgedAlarmCount} / 重点 {AlarmStatistics.Count(a => a.Count >= 3 || a.Active)}";
    public string TimeAxisSummary => $"采样点：最近 6 个周期 / 时间轴：{DateTime.Now.AddMinutes(-25):HH:mm} - {DateTime.Now:HH:mm}";
    public bool AllowManualCylinderWhenAuto => GetBoolParameter("自动运行允许手动气缸", false);
    public bool AllowManualStopperWhenAuto => GetBoolParameter("鑷姩杩愯鍏佽鎵嬪姩鎸″仠", false);
    public bool AllowRobotResetWhenRunning => GetBoolParameter("机械手运行时允许复位", false);
    public bool AllowAxisMoveWhenAlarm => GetBoolParameter("轴报警时允许运动", false);
    public double EstimatedDowntimeMinutes => AlarmStatistics.Where(a => a.Active || a.Count >= 3).Sum(a => a.Level switch { "Alarm" => a.Count * 12, "Error" => a.Count * 8, "Warning" => a.Count * 4, _ => a.Count * 2 });
    public int EstimatedProductionLoss => (int)Math.Round((EstimatedDowntimeMinutes / 60.0) * GetIntTag("Throughput_Hourly", 380));
    public string FocusAlarmHint => AlarmStatistics.FirstOrDefault()?.Message ?? "当前暂无重点报警";
    public string ProductionTrendPath => BuildSparklinePath(new double[] { Math.Max(0, ShiftProductionCount * 0.35), ShiftProductionCount * 0.5, ShiftProductionCount * 0.68, ShiftProductionCount * 0.8, ShiftProductionCount * 0.92, ShiftProductionCount });
    public string OeeTrendPath => BuildSparklinePath(new double[] { Math.Max(0, OeeRate - 9), OeeRate - 5, OeeRate - 3, OeeRate - 1, OeeRate + 1, OeeRate });
    public string AlarmTrendPath => BuildSparklinePath(new double[] { ActiveAlarmCount + 4, ActiveAlarmCount + 3, ActiveAlarmCount + 2, ActiveAlarmCount + 2, ActiveAlarmCount + 1, Math.Max(1, ActiveAlarmCount) });

    // ===== Phase 2.1 主界面 =====
    // H3 KPI mini sparkline（OEE 复用 OeeTrendPath；新加节拍 / UPH）
    public string CycleTimeTrendPath => BuildSparklinePath(new double[] { CycleTimeSeconds + 0.5, CycleTimeSeconds + 0.3, CycleTimeSeconds + 0.1, CycleTimeSeconds - 0.1, CycleTimeSeconds, CycleTimeSeconds + 0.1 });
    public string HourlyThroughputTrendPath => BuildSparklinePath(new double[] { Math.Max(0, HourlyThroughput * 0.85), HourlyThroughput * 0.9, HourlyThroughput * 0.95, HourlyThroughput * 0.98, HourlyThroughput, HourlyThroughput });

    // H1 目标完成度
    public double ProductionProgressPercent => TargetCount > 0 ? Math.Min(100, (double)ShiftProductionCount * 100 / TargetCount) : 0;
    public string ProductionProgressText => $"{ShiftProductionCount} / {TargetCount}";
    public string ProductionProgressPercentText => $"{ProductionProgressPercent:F1}%";

    // H2 班次倒计时（每次 _subscriptionTimer Tick 触发刷新）
    private (DateTime Start, DateTime End, string Label) ResolveCurrentShiftInfo()
    {
        // 默认 08:30 / 20:30；后续扩展从 IOptions<AppOptions>.Shift 读
        var dayStart = DateTime.Today.AddHours(8).AddMinutes(30);
        var nightStart = DateTime.Today.AddHours(20).AddMinutes(30);
        var now = DateTime.Now;
        if (now < dayStart) return (nightStart.AddDays(-1), dayStart, "夜班");
        if (now < nightStart) return (dayStart, nightStart, "白班");
        return (nightStart, dayStart.AddDays(1), "夜班");
    }
    public string CurrentShiftLabel => ResolveCurrentShiftInfo().Label;
    public string ShiftRemainingText
    {
        get
        {
            var info = ResolveCurrentShiftInfo();
            var remaining = info.End - DateTime.Now;
            if (remaining <= TimeSpan.Zero) return $"{info.Label} 切换中";
            var h = (int)remaining.TotalHours;
            return $"{info.Label}还剩 {h:D2}:{remaining.Minutes:D2}:{remaining.Seconds:D2}";
        }
    }

    // H5 启动条件 unmet 时显示哪条没过
    public string StartReadyDetail
    {
        get
        {
            var lines = new System.Collections.Generic.List<string>(3);
            lines.Add(StartModeReady ? "✓ 模式：已选择 (手动 / 自动)" : "✗ 模式：当前为调试 / 空运行 / 旁路，未选 手动 / 自动");
            lines.Add(StartAlarmReady ? "✓ 报警：无活动报警" : $"✗ 报警：存在 {ActiveAlarmCount} 个活动报警，需先复位");
            lines.Add(StartInterlockReady ? "✓ 联锁：条件满足" : "✗ 联锁：模式 / 报警未通过");
            return string.Join("\n", lines);
        }
    }

    // ===== Phase 2.2.A 监控-详细生产数据 =====
    // M5 自定义时间范围 DatePicker 仅在 "自定义" 选项时显示
    public bool IsCustomTimeRangeVisible => string.Equals(SelectedFlowTimeRange, "自定义", StringComparison.Ordinal);

    // M3 KPI 卡 ToolTip 明细（hover 显示更详细的统计）
    public string OrderKpiDetail =>
        $"工单号：{CurrentOrderText}\n本班次：{ShiftProductionCount} 件\n今日累计：{DailyProductionCount} 件\n良品/不良：{GoodCount} / {NgCount}";
    public string RecipeKpiDetail =>
        $"配方：{CurrentRecipeText}\n班次状态：{ShiftStatusText}\n节拍：{CycleTimeSeconds:F1}s\nUPH：{HourlyThroughput}";
    public string ShiftKpiDetail =>
        $"班次：{ShiftStatusText}\n本班产量：{ShiftProductionCount} / 目标 {TargetCount}\n达成率：{(TargetCount > 0 ? ShiftProductionCount * 100.0 / TargetCount : 0):F1}%\n良品 {ShiftGoodCount} / 不良 {ShiftNgCount}";
    public string TargetKpiDetail =>
        $"目标产量：{TargetCount} 件\n本班完成：{ShiftProductionCount}（{(TargetCount > 0 ? ShiftProductionCount * 100.0 / TargetCount : 0):F1}%）\n剩余：{Math.Max(0, TargetCount - ShiftProductionCount)} 件";

    // M6 工单 / 配方切换提示（"刚刚切换" / "5 分钟前切换"）
    public string OrderChangeHint => FormatChangeHint(LastOrderChangeAt);
    public string RecipeChangeHint => FormatChangeHint(LastRecipeChangeAt);
    private static string FormatChangeHint(DateTime? at)
    {
        if (at is null) return string.Empty;
        var elapsed = DateTime.Now - at.Value;
        if (elapsed.TotalSeconds < 60) return "● 刚刚切换";
        if (elapsed.TotalMinutes < 60) return $"● {(int)elapsed.TotalMinutes} 分钟前切换";
        if (elapsed.TotalHours < 24) return $"● {(int)elapsed.TotalHours} 小时前切换";
        return $"● {(int)elapsed.TotalDays} 天前切换";
    }
    public string ProgramMonitorMainFlowTracePath => BuildTracePath("F1", "主线1");
    public string ProgramMonitorSubFlow2TracePath => BuildTracePath("F2", "主线2");
    public string ProgramMonitorSubFlow3TracePath => BuildTracePath("F3", "主线3");
    public string ProgramMonitorTraceAxisTopLabel => GetTraceAxisLabel(3);
    public string ProgramMonitorTraceAxisMidHighLabel => GetTraceAxisLabel(2);
    public string ProgramMonitorTraceAxisMidLowLabel => GetTraceAxisLabel(1);
    public string ProgramMonitorTraceAxisBottomLabel => "0";
    public string ProgramMonitorTraceStartLabel => GetTraceStart().ToString("HH:mm:ss");
    public string ProgramMonitorTraceEndLabel => GetTraceEnd().ToString("HH:mm:ss");
    public string ProgramMonitorTraceLatestText => BuildMainFlowTraceLatestText();
    public string ProgramMonitorTraceWindowText => $"窗口：最近 {ProgramMonitorTraceWindowMinutes:F0} 分钟";
    public bool HasProgramMonitorTraceHistory => _programMonitorTraceHistory.Count > 0;
    public string FlowStepTrendPath => BuildSparklinePath(FlowSteps.Take(6).Select(f => (double)f.StepNo).Reverse().DefaultIfEmpty(0));
    public string FlowIssueTrendPath => BuildSparklinePath(FlowIssueSummaries.Select((x, i) => (double)(i + 1) * 10).DefaultIfEmpty(0));
    public string FlowRankingSummary => BuildFlowRankingSummary();
    public string CurrentMonitorTitle => CurrentMonitorSubSection;
    public string CurrentManualTitle => CurrentManualSubSection;
    public string CurrentParameterTitle => CurrentParameterSubSection == "系统参数设定" ? "HMI变量和PLC变量绑定" : CurrentParameterSubSection;
    public string CurrentAlarmTitle => CurrentAlarmSubSection;
    public bool HasGeneratedIoPrograms => GeneratedIoPrograms.Count > 0;
    public string SelectedGeneratedIoProgramContent => SelectedGeneratedIoProgram?.Content ?? "生成完成后，程序预览会显示在这里。";
    public string IoGenerationHeadline => $"PLC 模板：{SelectedIoPlcTemplate} / 工位号：{IoOperationNumber}";
    public string IoGenerationCountSummary => $"输入 {IoTableRows.Count(r => !string.IsNullOrWhiteSpace(r.InputAddress))} 点 / 输出 {IoTableRows.Count(r => !string.IsNullOrWhiteSpace(r.OutputAddress))} 点";
    public IReadOnlyList<string> SfcDeviceTypes => SfcCodeGeneratorService.DeviceTypes;

    public IEnumerable<SfcDeviceOption> SfcCylinderOptions =>
        ManualCylinderBlocks
            .GroupBy(c => c.CylinderIndex)
            .Select(g =>
            {
                var c = g.First();
                return new SfcDeviceOption("Cylinder", g.Key, c.DisplayName, c.WorkCommandLabel, c.HomeCommandLabel);
            });

    public IEnumerable<SfcDeviceOption> SfcAxisOptions =>
        ManualAxisBlocks
            .GroupBy(a => a.AxisIndex)
            .Select(g => new SfcDeviceOption("Axis", g.Key, g.First().DisplayName));

    public IEnumerable<SfcDeviceOption> SfcVacuumOptions
    {
        get
        {
            var seen = new Dictionary<int, SfcDeviceOption>();
            foreach (var row in IoTableRows)
            {
                TryAddVacuumOption(seen, row.OutputComment);
                TryAddVacuumOption(seen, row.InputComment);
            }
            return seen.Values.OrderBy(o => o.Index);
        }
    }

    private static void TryAddVacuumOption(Dictionary<int, SfcDeviceOption> seen, string? comment)
    {
        if (string.IsNullOrWhiteSpace(comment)) return;
        var m = System.Text.RegularExpressions.Regex.Match(comment, @"VAC(\d{1,3})", RegexOptions.IgnoreCase);
        if (!m.Success) return;
        var idx = int.Parse(m.Groups[1].Value);
        if (seen.ContainsKey(idx)) return;
        // Strip the raw key suffix to get a clean display name
        var displayName = comment.Trim();
        seen[idx] = new SfcDeviceOption("Vacuum", idx, displayName);
    }
    // 只要当前有 IO 表内容即可保存：
    // - 若之前导入过（或已从配置恢复来源路径）则保存到原目录；
    // - 否则保存到默认目录（config/IoTable），保证重启后也能一直有效。
    public bool CanSaveIoTable => !string.IsNullOrWhiteSpace(_currentIoSourceFilePath) || IoTableRows.Count > 0;
    /// <summary>
    /// 用户画布页面跳转时的"父导航组"覆盖：非空时 CurrentNavigationGroup 优先返回此值。
    /// 用于挂在内置段下的画布页：内容显示运行页 Tab，但侧栏保持父段（如"手动操作"）。
    /// </summary>
    private string? _navigationGroupOverride;

    public void SetNavigationGroupOverride(string? group)
    {
        if (string.Equals(_navigationGroupOverride, group, StringComparison.Ordinal)) return;
        _navigationGroupOverride = string.IsNullOrWhiteSpace(group) ? null : group;
        OnPropertyChanged(nameof(CurrentNavigationGroup));
    }

    public string CurrentNavigationGroup =>
        _navigationGroupOverride ?? ResolveNavigationGroup(CurrentSection);
    public bool IsMonitorIoPageVisible => string.Equals(CurrentMonitorSubSection, "输入输出监控", StringComparison.Ordinal);
    public bool IsMonitorProgramPageVisible => string.Equals(CurrentMonitorSubSection, "程序监控", StringComparison.Ordinal);
    public bool IsMonitorCommunicationPageVisible => string.Equals(CurrentMonitorSubSection, "通讯状态监控", StringComparison.Ordinal);
    public bool IsMonitorProductionPageVisible => string.Equals(CurrentMonitorSubSection, "详细生产数据", StringComparison.Ordinal);
    public bool IsMonitorSinglePanelPageVisible => !IsMonitorProductionPageVisible;
    public bool IsManualCylinderPageVisible => string.Equals(CurrentManualSubSection, "气缸", StringComparison.Ordinal);
    public bool IsManualAxisPageVisible => string.Equals(CurrentManualSubSection, "轴", StringComparison.Ordinal);
    public bool IsManualRobotPageVisible => string.Equals(CurrentManualSubSection, "机械手", StringComparison.Ordinal);
    public bool IsManualMotorPageVisible => string.Equals(CurrentManualSubSection, "电机", StringComparison.Ordinal);
    public bool IsManualStopperPageVisible => string.Equals(CurrentManualSubSection, "挡停", StringComparison.Ordinal);
    public bool IsParameterSystemPageVisible => string.Equals(CurrentParameterSubSection, "系统参数设定", StringComparison.Ordinal);
    public bool IsParameterAxisPageVisible => string.Equals(CurrentParameterSubSection, "轴参数设定", StringComparison.Ordinal);
    public bool IsParameterCylinderPageVisible => string.Equals(CurrentParameterSubSection, "气缸参数设定", StringComparison.Ordinal);
    public bool IsParameterVacuumPageVisible => string.Equals(CurrentParameterSubSection, "真空参数设定", StringComparison.Ordinal);
    public bool IsParameterSensorPageVisible => string.Equals(CurrentParameterSubSection, "传感器参数设定", StringComparison.Ordinal);
    public bool IsAlarmCurrentPageVisible => string.Equals(CurrentAlarmSubSection, "当前报警", StringComparison.Ordinal);
    public bool IsAlarmHistoryPageVisible => string.Equals(CurrentAlarmSubSection, "历史报警", StringComparison.Ordinal);
    public bool IsAlarmLogPageVisible => string.Equals(CurrentAlarmSubSection, "日志", StringComparison.Ordinal);
    public bool IsAlarmStatisticsPageVisible => string.Equals(CurrentAlarmSubSection, "报警统计", StringComparison.Ordinal);
    public string CurrentDesignerTitle => CurrentDesignerSubSection;
    public bool IsDesignerCanvasPageVisible => string.Equals(CurrentDesignerSubSection, "画布设计", StringComparison.Ordinal);
    public bool IsDesignerIoProgramPageVisible => string.Equals(CurrentDesignerSubSection, "手动程序生成", StringComparison.Ordinal);
    public bool IsDesignerAutoProgramPageVisible => string.Equals(CurrentDesignerSubSection, "自动程序生成", StringComparison.Ordinal);
    public bool IsDesignerInitProgramPageVisible => string.Equals(CurrentDesignerSubSection, "初始化程序生成", StringComparison.Ordinal);
    public bool IsSelectedDesignerElementButtonLike => SelectedDesignerElement is not null
        && SelectedDesignerElement.ElementType is "Button" or "Motor" or "Cylinder" or "Stopper" or "Robot" or "PageButton";
    public bool IsSelectedDesignerElementTagBindable => SelectedDesignerElement is not null
        && SelectedDesignerElement.ElementType is "Button" or "Indicator" or "ValueDisplay" or "AlarmBanner" or "Motor" or "Cylinder" or "Axis" or "Robot" or "Stopper";
    public bool IsSelectedDesignerElementNavigationAction => SelectedDesignerElement?.CommandBinding == "页面跳转";

    partial void OnIsRuntimeModeChanged(bool value)
    {
        OnPropertyChanged(nameof(DesignerModeText));
        OnPropertyChanged(nameof(IsDesignMode));
        OnPropertyChanged(nameof(IsRuntimeDashboardVisible));
        OnPropertyChanged(nameof(RuntimeHeaderText));
        SystemMessage = value ? "已切换到运行态" : "已切换到设计态";
        // 设计态 → 运行态：直接跳到主界面，方便看到发布后的运行效果
        if (value)
        {
            NavigateCommand.Execute("主界面");
        }
        _ = UpdateAutoRefreshStateAsync();
    }

    // H6 / H7 流程日志 Filter 应用
    partial void OnFlowLogShowInfoChanged(bool value) => ApplyFlowLogFilter();
    partial void OnFlowLogShowWarnChanged(bool value) => ApplyFlowLogFilter();
    partial void OnFlowLogShowErrorChanged(bool value) => ApplyFlowLogFilter();
    partial void OnFlowLogSearchTextChanged(string value) => ApplyFlowLogFilter();

    private void ApplyFlowLogFilter()
    {
        if (FlowStepsView is null) return;
        FlowStepsView.Filter = obj =>
        {
            if (obj is not Models.FlowStepRecord step) return true;
            var resultLower = (step.Result ?? string.Empty).ToLowerInvariant();
            var commentLower = (step.Comment ?? string.Empty).ToLowerInvariant();
            var isError = resultLower.Contains("error") || resultLower.Contains("超时") || resultLower.Contains("失败")
                          || resultLower.Contains("ng") || commentLower.Contains("失败") || commentLower.Contains("超时");
            var isWarn = !isError && (resultLower.Contains("warn") || resultLower.Contains("warning")
                          || resultLower.Contains("慢") || commentLower.Contains("等待") || resultLower.Contains("运行中"));
            var isInfo = !isError && !isWarn;
            if (isError && !FlowLogShowError) return false;
            if (isWarn && !FlowLogShowWarn) return false;
            if (isInfo && !FlowLogShowInfo) return false;
            if (!string.IsNullOrEmpty(FlowLogSearchText))
            {
                var s = FlowLogSearchText.ToLowerInvariant();
                var combined = $"{step.FlowName} {step.StepNo} {step.Result} {step.RelatedAlarm} {step.Comment} {step.Title}".ToLowerInvariant();
                if (!combined.Contains(s)) return false;
            }
            return true;
        };
    }

    // MA6 双击气缸名 → 跳到参数设定页（后续 P1 完成后会自动按设备名过滤参数表）
    [RelayCommand]
    private void JumpToParameterByDevice(object? device)
    {
        var deviceName = device switch
        {
            Models.ManualCylinderBlockItem cyl => cyl.DisplayName,
            Models.ManualAxisBlockItem axis => axis.DisplayName,
            string s => s,
            _ => string.Empty,
        };
        Navigate("参数设定");
        if (!string.IsNullOrWhiteSpace(deviceName))
            SystemMessage = $"已跳转到参数页：{deviceName}";
    }

    // H4 重点报警跳转：点击主界面"重点报警"卡 → 跳报警页对应记录
    [RelayCommand]
    private void JumpToFocusAlarm()
    {
        var alarm = AlarmStatistics.FirstOrDefault();
        var keyword = alarm?.Source ?? alarm?.Message ?? string.Empty;
        if (string.IsNullOrEmpty(keyword))
        {
            Navigate("报警画面");
            return;
        }
        JumpToAlarmByKeyword(keyword);
    }

    partial void OnCurrentUserRoleChanged(UserRole value)
    {
        LoginUser = CurrentRoleText;
        OnPropertyChanged(nameof(CanEditParameters));
        OnPropertyChanged(nameof(CanAdmin));
        OnPropertyChanged(nameof(CanOperateDevices));
        OnPropertyChanged(nameof(CurrentRoleText));
        OnPropertyChanged(nameof(CurrentAccount));
        OnPropertyChanged(nameof(LastLoginText));
        OnPropertyChanged(nameof(FailedAttemptsText));
        RefreshParameterPermissions();
    }

    /// <summary>当前角色对应的账号（从 IUserService 查找）。</summary>
    public UserAccount? CurrentAccount =>
        _userService?.ListUsers().FirstOrDefault(u => u.Role == CurrentUserRole);

    // ========== G7 全局未保存提示 ==========
    private readonly List<Func<bool>> _dirtySources = new();

    /// <summary>子模块（画布设计 / 参数 / 配方）注册自己的 dirty 探测函数。</summary>
    public void RegisterDirtySource(Func<bool> isDirty)
    {
        if (isDirty is null) return;
        _dirtySources.Add(isDirty);
    }

    /// <summary>子模块的 dirty 状态变化时调用，触发 IsAnyDirty 的 PropertyChanged。</summary>
    public void NotifyDirtyStateChanged() => OnPropertyChanged(nameof(IsAnyDirty));

    /// <summary>当前是否存在任意未保存改动（聚合所有已注册的 dirty 源）。</summary>
    public bool IsAnyDirty => _dirtySources.Any(f => f());

    /// <summary>当前账号的最近一次成功登录时间，用于 LoginView 显示。</summary>
    public string LastLoginText =>
        CurrentAccount?.LastLoginAt is { } at
            ? $"上次登录：{at:yyyy-MM-dd HH:mm}"
            : "首次登录或未记录";

    /// <summary>自上次成功登录以来的失败尝试次数，&gt; 0 时显示。</summary>
    public string FailedAttemptsText =>
        (CurrentAccount?.FailedAttempts ?? 0) > 0
            ? $"自上次成功后失败：{CurrentAccount!.FailedAttempts} 次"
            : string.Empty;

    partial void OnCurrentMonitorSubSectionChanged(string value)
    {
        OnPropertyChanged(nameof(CurrentMonitorTitle));
        OnPropertyChanged(nameof(IsMonitorIoPageVisible));
        OnPropertyChanged(nameof(IsMonitorProgramPageVisible));
        OnPropertyChanged(nameof(IsMonitorCommunicationPageVisible));
        OnPropertyChanged(nameof(IsMonitorProductionPageVisible));
        OnPropertyChanged(nameof(IsMonitorSinglePanelPageVisible));
    }

    partial void OnCurrentManualSubSectionChanged(string value)
    {
        OnPropertyChanged(nameof(CurrentManualTitle));
        OnPropertyChanged(nameof(IsManualCylinderPageVisible));
        OnPropertyChanged(nameof(IsManualAxisPageVisible));
        OnPropertyChanged(nameof(IsManualRobotPageVisible));
        OnPropertyChanged(nameof(IsManualMotorPageVisible));
        OnPropertyChanged(nameof(IsManualStopperPageVisible));
        // 切换到机械手页面时刷新状态
        if (IsManualRobotPageVisible && RobotControlViewModel is not null)
        {
            _ = RefreshRobotStatusAsync();
        }
        // 路径B：若启用了设计器布局，按子页签加载对应 manual.* 页
        if (this is ApexHMI.ViewModels.Shell.MainWindowViewModel mvm)
        {
            _ = mvm.LoadManualPageForCurrentSubSectionAsync();
        }
        // 子页面切换后立即刷新
        if (AutoRefreshEnabled && _opcUaService.IsConnected)
        {
            _ = AutoRefreshTickAsync();
        }
    }

    private async Task RefreshRobotStatusAsync()
    {
        if (RobotControlViewModel is null) return;
        // TODO: 从PLC读取机械手实际状态
        await Task.CompletedTask;
    }

    partial void OnCurrentParameterSubSectionChanged(string value)
    {
        OnPropertyChanged(nameof(CurrentParameterTitle));
        OnPropertyChanged(nameof(IsParameterSystemPageVisible));
        OnPropertyChanged(nameof(IsParameterAxisPageVisible));
        OnPropertyChanged(nameof(IsParameterCylinderPageVisible));
        OnPropertyChanged(nameof(IsParameterVacuumPageVisible));
        OnPropertyChanged(nameof(IsParameterSensorPageVisible));
        ParametersView.Refresh();
        // 子页面切换后立即刷新
        if (AutoRefreshEnabled && _opcUaService.IsConnected)
        {
            _ = AutoRefreshTickAsync();
        }
    }

    partial void OnCurrentAlarmSubSectionChanged(string value)
    {
        OnPropertyChanged(nameof(CurrentAlarmTitle));
        OnPropertyChanged(nameof(IsAlarmCurrentPageVisible));
        OnPropertyChanged(nameof(IsAlarmHistoryPageVisible));
        OnPropertyChanged(nameof(IsAlarmLogPageVisible));
        OnPropertyChanged(nameof(IsAlarmStatisticsPageVisible));
    }

    partial void OnCurrentDesignerSubSectionChanged(string value)
    {
        OnPropertyChanged(nameof(CurrentDesignerTitle));
        OnPropertyChanged(nameof(IsDesignerCanvasPageVisible));
        OnPropertyChanged(nameof(IsDesignerIoProgramPageVisible));
        OnPropertyChanged(nameof(IsDesignerAutoProgramPageVisible));
        OnPropertyChanged(nameof(IsDesignerInitProgramPageVisible));
    }

    partial void OnSelectedSfcStepChanged(SfcStep? value)
    {
        _prevSfcStep = value;
    }

    partial void OnCurrentSectionChanged(string value)
    {
        OnPropertyChanged(nameof(CurrentNavigationGroup));
        var targetTabIndex = ResolveTabIndex(value);
        if (SelectedTabIndex != targetTabIndex)
        {
            SelectedTabIndex = targetTabIndex;
        }
        // 页面切换后立即刷新当前页面的变量
        if (AutoRefreshEnabled && _opcUaService.IsConnected)
        {
            _ = AutoRefreshTickAsync();
        }
    }

    partial void OnSelectedTabIndexChanged(int value)
    {
        var section = GetSectionNameByTabIndex(value);
        if (ResolveTabIndex(CurrentSection) == value)
        {
            return;
        }

        if (!string.Equals(CurrentSection, section, StringComparison.Ordinal))
        {
            Navigate(section);
        }
    }

    partial void OnSelectedOpcUaBrowseNodeChanged(OpcUaBrowseNode? value)
    {
        if (value is null || value.IsPlaceholder)
        {
            return;
        }

        _ = RefreshSelectedOpcUaNodeAsync();
    }

    partial void OnSelectedMonitorCategoryChanged(string value) => RefreshMonitorView();
    partial void OnSelectedAlarmLevelChanged(string value) => RefreshAlarmStatistics();
    partial void OnSelectedAlarmTimeRangeChanged(string value) => RefreshAlarmStatistics();
    partial void OnShowOnlyFocusAlarmsChanged(bool value) => RefreshAlarmStatistics();
    partial void OnSelectedFlowFilterChanged(string value) => RefreshFlowView();
    partial void OnSelectedFlowTimeRangeChanged(string value)
    {
        OnPropertyChanged(nameof(IsCustomTimeRangeVisible));
        RefreshFlowView();
    }
    partial void OnSelectedFlowStepFilterChanged(string value) => RefreshFlowView();
    partial void OnShowOnlyAbnormalFlowChanged(bool value) => RefreshFlowView();
    partial void OnProgramMonitorTraceWindowMinutesChanged(double value) => RefreshProgramMonitorTrace();
    partial void OnProgramMonitorTraceShowLine1Changed(bool value) => RefreshProgramMonitorTrace();
    partial void OnProgramMonitorTraceShowLine2Changed(bool value) => RefreshProgramMonitorTrace();
    partial void OnProgramMonitorTraceShowLine3Changed(bool value) => RefreshProgramMonitorTrace();
    partial void OnSelectedProgramMonitorTraceFlowChanged(string value) => RefreshProgramMonitorTrace();
    partial void OnProgramMonitorTraceReplayPositionChanged(int value)
    {
        if (!ProgramMonitorTraceReplayMode || _programMonitorTraceHistory.Count == 0)
        {
            return;
        }

        value = Math.Max(0, Math.Min(ProgramMonitorTraceReplayMaximum, value));
        _programMonitorTraceFollowNow = false;
        _programMonitorTraceWindowEnd = _programMonitorTraceHistory[value].Time;
        RefreshProgramMonitorTrace();
    }
    partial void OnSelectedIoPlcTemplateChanged(string value) => OnPropertyChanged(nameof(IoGenerationHeadline));
    partial void OnIoOperationNumberChanged(string value)
    {
        OnPropertyChanged(nameof(IoGenerationHeadline));
        RebindCylinderDbByOperation();
    }
    partial void OnCylinderHomeMaskEnabledChanged(bool value) => OnPropertyChanged(nameof(CylinderHomeMaskButtonText));
    partial void OnCylinderWorkMaskEnabledChanged(bool value) => OnPropertyChanged(nameof(CylinderWorkMaskButtonText));
    partial void OnCylinderConfiguredNameChanged(string value) => OnPropertyChanged(nameof(CylinderDisplayName));
    partial void OnCylinderHomeCommandTagNameChanged(string value) => RefreshCylinderBindingProperties();
    partial void OnCylinderWorkCommandTagNameChanged(string value) => RefreshCylinderBindingProperties();
    partial void OnCylinderHomeSensorTagNameChanged(string value) => RefreshCylinderBindingProperties();
    partial void OnCylinderWorkSensorTagNameChanged(string value) => RefreshCylinderBindingProperties();
    partial void OnCylinderHomeInterlockTagNameChanged(string value) => RefreshCylinderBindingProperties();
    partial void OnCylinderWorkInterlockTagNameChanged(string value) => RefreshCylinderBindingProperties();
    partial void OnSelectedCylinderSettingsBlockChanged(ManualCylinderBlockItem? value)
    {
        LoadSelectedCylinderParmSettings();
        RefreshCylinderBindingProperties();
    }
    partial void OnSelectedAxisSettingsBlockChanged(ManualAxisBlockItem? value)
    {
        RefreshManualAxisBlockStates();
        RefreshAxisBindingProperties();
    }
    partial void OnCylinderAlarmTimeSettingChanged(string value)
    {
        if (_isLoadingSelectedCylinderParmSettings) return;
        _ = WriteSelectedCylinderNumericParmAsync(SelectedCylinderErrorDelayBinding, value, "已更新气缸报警时间参数");
    }
    partial void OnCylinderHomeDelaySettingChanged(string value)
    {
        if (_isLoadingSelectedCylinderParmSettings) return;
        _ = WriteSelectedCylinderNumericParmAsync(SelectedCylinderHomeDelayBinding, value, "已更新气缸原点延时参数");
    }
    partial void OnCylinderWorkDelaySettingChanged(string value)
    {
        if (_isLoadingSelectedCylinderParmSettings) return;
        _ = WriteSelectedCylinderNumericParmAsync(SelectedCylinderWorkDelayBinding, value, "已更新气缸动点延时参数");
    }
    partial void OnSelectedDesignerElementChanged(DesignerElement? value)
    {
        if (_designerSelectionSubscription is not null)
        {
            _designerSelectionSubscription.PropertyChanged -= SelectedDesignerElement_PropertyChanged;
        }

        _designerSelectionSubscription = value;
        if (_designerSelectionSubscription is not null)
        {
            _designerSelectionSubscription.PropertyChanged += SelectedDesignerElement_PropertyChanged;
        }

        OnPropertyChanged(nameof(IsSelectedDesignerElementButtonLike));
        OnPropertyChanged(nameof(IsSelectedDesignerElementTagBindable));
        OnPropertyChanged(nameof(IsSelectedDesignerElementNavigationAction));
    }

    private void SelectedDesignerElement_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DesignerElement.CommandBinding) or nameof(DesignerElement.ElementType))
        {
            OnPropertyChanged(nameof(IsSelectedDesignerElementButtonLike));
            OnPropertyChanged(nameof(IsSelectedDesignerElementTagBindable));
            OnPropertyChanged(nameof(IsSelectedDesignerElementNavigationAction));
        }
    }

    private void RefreshCylinderBindingProperties()
    {
        OnPropertyChanged(nameof(CylinderStatusText));
        OnPropertyChanged(nameof(CylinderForwardActive));
        OnPropertyChanged(nameof(CylinderBackwardActive));
        OnPropertyChanged(nameof(CylinderOutputActive));
        OnPropertyChanged(nameof(CylinderOutputText));
        OnPropertyChanged(nameof(CylinderCurrentStateText));
        OnPropertyChanged(nameof(CylinderDisplayName));
        OnPropertyChanged(nameof(CylinderActionHint));
        OnPropertyChanged(nameof(CylinderInterlockBaseReady));
        OnPropertyChanged(nameof(CylinderHomeInterlockActive));
        OnPropertyChanged(nameof(CylinderMoveInterlockActive));
        OnPropertyChanged(nameof(CylinderHomeInterlockText));
        OnPropertyChanged(nameof(CylinderMoveInterlockText));
        OnPropertyChanged(nameof(CylinderInterlockHint));
        OnPropertyChanged(nameof(SelectedCylinderHomeCommandBinding));
        OnPropertyChanged(nameof(SelectedCylinderWorkCommandBinding));
        OnPropertyChanged(nameof(SelectedCylinderHomeSensorBinding));
        OnPropertyChanged(nameof(SelectedCylinderWorkSensorBinding));
        OnPropertyChanged(nameof(SelectedCylinderHomeInterlockBinding));
        OnPropertyChanged(nameof(SelectedCylinderWorkInterlockBinding));
        OnPropertyChanged(nameof(SelectedCylinderHomeDisplayBinding));
        OnPropertyChanged(nameof(SelectedCylinderWorkDisplayBinding));
        OnPropertyChanged(nameof(SelectedCylinderHomeDisplayFallbackBinding));
        OnPropertyChanged(nameof(SelectedCylinderWorkDisplayFallbackBinding));
        OnPropertyChanged(nameof(SelectedCylinderDisableHomeBinding));
        OnPropertyChanged(nameof(SelectedCylinderDisableWorkBinding));
        OnPropertyChanged(nameof(SelectedCylinderErrorDelayBinding));
        OnPropertyChanged(nameof(SelectedCylinderHomeDelayBinding));
        OnPropertyChanged(nameof(SelectedCylinderWorkDelayBinding));
        OnPropertyChanged(nameof(SelectedCylinderHomeCommandValue));
        OnPropertyChanged(nameof(SelectedCylinderWorkCommandValue));
        OnPropertyChanged(nameof(SelectedCylinderHomeSensorValue));
        OnPropertyChanged(nameof(SelectedCylinderWorkSensorValue));
        OnPropertyChanged(nameof(SelectedCylinderHomeInterlockValue));
        OnPropertyChanged(nameof(SelectedCylinderWorkInterlockValue));
        OnPropertyChanged(nameof(SelectedCylinderHomeDisplayValue));
        OnPropertyChanged(nameof(SelectedCylinderWorkDisplayValue));
        OnPropertyChanged(nameof(SelectedCylinderHomeDisplayFallbackValue));
        OnPropertyChanged(nameof(SelectedCylinderWorkDisplayFallbackValue));
    }

    private void RefreshAxisBindingProperties()
    {
        OnPropertyChanged(nameof(SelectedAxisPowerCommandBinding));
        OnPropertyChanged(nameof(SelectedAxisStopCommandBinding));
        OnPropertyChanged(nameof(SelectedAxisHomeCommandBinding));
        OnPropertyChanged(nameof(SelectedAxisJogForwardBinding));
        OnPropertyChanged(nameof(SelectedAxisJogBackwardBinding));
        OnPropertyChanged(nameof(SelectedAxisStartPositionCommandBinding));
        OnPropertyChanged(nameof(SelectedAxisReferenceCommandBinding));
        OnPropertyChanged(nameof(SelectedAxisTeachEnableBinding));
        OnPropertyChanged(nameof(SelectedAxisTeachWriteBinding));
        OnPropertyChanged(nameof(SelectedAxisPointSelectBinding));
        OnPropertyChanged(nameof(SelectedAxisMoveToPointBinding));
        OnPropertyChanged(nameof(SelectedAxisSetPositionBinding));
        OnPropertyChanged(nameof(SelectedAxisSetVelocityBinding));
        OnPropertyChanged(nameof(SelectedAxisHomeSignalBinding));
        OnPropertyChanged(nameof(SelectedAxisPositiveLimitBinding));
        OnPropertyChanged(nameof(SelectedAxisNegativeLimitBinding));
        OnPropertyChanged(nameof(SelectedAxisServoFeedbackBinding));
        OnPropertyChanged(nameof(SelectedAxisPowerOnBinding));
        OnPropertyChanged(nameof(SelectedAxisBusyBinding));
        OnPropertyChanged(nameof(SelectedAxisPosOkBinding));
        OnPropertyChanged(nameof(SelectedAxisInitializedBinding));
        OnPropertyChanged(nameof(SelectedAxisHomeInterlockBinding));
        OnPropertyChanged(nameof(SelectedAxisJogInterlockBinding));
        OnPropertyChanged(nameof(SelectedAxisPositionInterlockBinding));
        OnPropertyChanged(nameof(SelectedAxisErrorBinding));
        OnPropertyChanged(nameof(SelectedAxisErrorIdBinding));
        OnPropertyChanged(nameof(SelectedAxisActualPositionBinding));
        OnPropertyChanged(nameof(SelectedAxisActualVelocityBinding));
        OnPropertyChanged(nameof(SelectedAxisActualTorqueBinding));
        OnPropertyChanged(nameof(SelectedAxisStateBinding));
        OnPropertyChanged(nameof(SelectedAxisPowerCommandValue));
        OnPropertyChanged(nameof(SelectedAxisStopCommandValue));
        OnPropertyChanged(nameof(SelectedAxisHomeCommandValue));
        OnPropertyChanged(nameof(SelectedAxisJogForwardValue));
        OnPropertyChanged(nameof(SelectedAxisJogBackwardValue));
        OnPropertyChanged(nameof(SelectedAxisStartPositionCommandValue));
        OnPropertyChanged(nameof(SelectedAxisReferenceCommandValue));
        OnPropertyChanged(nameof(SelectedAxisTeachEnableValue));
        OnPropertyChanged(nameof(SelectedAxisTeachWriteValue));
        OnPropertyChanged(nameof(SelectedAxisPointSelectValue));
        OnPropertyChanged(nameof(SelectedAxisMoveToPointValue));
        OnPropertyChanged(nameof(SelectedAxisSetPositionValue));
        OnPropertyChanged(nameof(SelectedAxisSetVelocityValue));
        OnPropertyChanged(nameof(SelectedAxisHomeSignalValue));
        OnPropertyChanged(nameof(SelectedAxisPositiveLimitValue));
        OnPropertyChanged(nameof(SelectedAxisNegativeLimitValue));
        OnPropertyChanged(nameof(SelectedAxisServoFeedbackValue));
        OnPropertyChanged(nameof(SelectedAxisPowerOnValue));
        OnPropertyChanged(nameof(SelectedAxisBusyValue));
        OnPropertyChanged(nameof(SelectedAxisPosOkValue));
        OnPropertyChanged(nameof(SelectedAxisInitializedValue));
        OnPropertyChanged(nameof(SelectedAxisHomeInterlockValue));
        OnPropertyChanged(nameof(SelectedAxisJogInterlockValue));
        OnPropertyChanged(nameof(SelectedAxisPositionInterlockValue));
        OnPropertyChanged(nameof(SelectedAxisErrorValue));
        OnPropertyChanged(nameof(SelectedAxisErrorIdValue));
        OnPropertyChanged(nameof(SelectedAxisActualPositionValue));
        OnPropertyChanged(nameof(SelectedAxisActualVelocityValue));
        OnPropertyChanged(nameof(SelectedAxisActualTorqueValue));
        OnPropertyChanged(nameof(SelectedAxisStateValue));
        OnPropertyChanged(nameof(SelectedAxisCurrentStateText));
        OnPropertyChanged(nameof(SelectedAxisInterlockHint));
    }

    private void RefreshCylinderMaskStates()
    {
        // 更新屏蔽按钮状态 - 这些属性控制按钮的显示状态
        if (!_isLoadingSelectedCylinderParmSettings)
        {
            CylinderHomeMaskEnabled = GetBoolTag(SelectedCylinderDisableHomeBinding);
            CylinderWorkMaskEnabled = GetBoolTag(SelectedCylinderDisableWorkBinding);
        }
    }

    private void LoadSelectedCylinderParmSettings()
    {
        _isLoadingSelectedCylinderParmSettings = true;
        try
        {
            CylinderHomeMaskEnabled = GetBoolTag(SelectedCylinderDisableHomeBinding);
            CylinderWorkMaskEnabled = GetBoolTag(SelectedCylinderDisableWorkBinding);
            CylinderAlarmTimeSetting = GetTagValueOrFallback(SelectedCylinderErrorDelayBinding, "20");
            CylinderHomeDelaySetting = GetTagValueOrFallback(SelectedCylinderHomeDelayBinding, "3");
            CylinderWorkDelaySetting = GetTagValueOrFallback(SelectedCylinderWorkDelayBinding, "3");
        }
        finally
        {
            _isLoadingSelectedCylinderParmSettings = false;
        }
    }

    private string GetSelectedCylinderParmTagName(string parmFieldName)
    {
        if (SelectedCylinderSettingsBlock is null)
        {
            return string.Empty;
        }

        var root = ResolveCylinderBlockRoot(SelectedCylinderSettingsBlock);
        return string.IsNullOrWhiteSpace(root) ? string.Empty : $"{root}.Parm.{parmFieldName}";
    }

    private string GetSelectedCylinderDisplayTagName(string fieldName)
    {
        if (SelectedCylinderSettingsBlock is null)
        {
            return string.Empty;
        }

        var root = ResolveCylinderBlockRoot(SelectedCylinderSettingsBlock);
        return string.IsNullOrWhiteSpace(root) ? string.Empty : $"{root}.{fieldName}";
    }

    private string GetTagValueOrFallback(string tagNameOrNodeId, string fallback)
    {
        var value = GetTagValue(tagNameOrNodeId);
        return string.IsNullOrWhiteSpace(value) || value == "--" || value.StartsWith("ERR:", StringComparison.OrdinalIgnoreCase)
            ? fallback
            : value;
    }

    private async Task WriteSelectedCylinderBoolParmAsync(string tagNameOrNodeId, bool value, string successMessage)
    {
        if (string.IsNullOrWhiteSpace(tagNameOrNodeId))
        {
            return;
        }

        var tag = EnsureWritableCylinderParmTag(tagNameOrNodeId, "Boolean");
        try
        {
            if (_opcUaService.IsConnected)
            {
                await _opcUaService.WriteTagAsync(tag, value);
            }

            tag.CurrentValue = value.ToString();
            SystemMessage = successMessage;
        }
        catch (Exception ex)
        {
            SystemMessage = $"气缸参数写入失败：{ex.Message}";
            AddLog("气缸参数", SystemMessage, "Error");
        }

        RefreshCylinderBindingProperties();
    }

    private async Task WriteSelectedCylinderNumericParmAsync(string tagNameOrNodeId, string textValue, string successMessage)
    {
        if (_isLoadingSelectedCylinderParmSettings || string.IsNullOrWhiteSpace(tagNameOrNodeId))
        {
            return;
        }

        if (!ushort.TryParse(textValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numericValue))
        {
            if (!string.IsNullOrWhiteSpace(textValue))
            {
                SystemMessage = "气缸参数格式错误，请输入 0-65535 的整数。";
            }
            return;
        }

        var tag = EnsureWritableCylinderParmTag(tagNameOrNodeId, "UInt16");
        try
        {
            if (_opcUaService.IsConnected)
            {
                await _opcUaService.WriteTagAsync(tag, numericValue);
            }

            tag.CurrentValue = numericValue.ToString(CultureInfo.InvariantCulture);
            SystemMessage = successMessage;
        }
        catch (Exception ex)
        {
            SystemMessage = $"气缸参数写入失败：{ex.Message}";
            AddLog("气缸参数", SystemMessage, "Error");
        }

        RefreshCylinderBindingProperties();
    }

    private TagItem EnsureWritableCylinderParmTag(string tagNameOrNodeId, string dataType)
    {
        var existing = FindTagByNameOrNodeId(tagNameOrNodeId);
        if (existing is not null)
        {
            existing.IsWritable = true;
            return existing;
        }

        var tag = new TagItem
        {
            Name = tagNameOrNodeId.Replace("Application.", string.Empty, StringComparison.OrdinalIgnoreCase),
            NodeId = tagNameOrNodeId,
            DataType = dataType,
            Category = "Cylinder",
            Group = "Parm",
            Direction = "Output",
            CurrentValue = "0",
            Description = "气缸参数页自动补齐的 Parm 变量",
            IsWritable = true
        };

        Tags.Add(tag);
        return tag;
    }
    partial void OnIoSaveIntervalMinutesChanged(int value)
    {
        if (value < 1)
        {
            IoSaveIntervalMinutes = 1;
        }
    }
    partial void OnSelectedGeneratedIoProgramChanged(GeneratedProgramArtifact? value) => OnPropertyChanged(nameof(SelectedGeneratedIoProgramContent));
    partial void OnAutoRefreshEnabledChanged(bool value) => _ = UpdateAutoRefreshStateAsync();
    partial void OnUseOpcSubscriptionChanged(bool value) => _ = UpdateAutoRefreshStateAsync();
    partial void OnSelectedRecipeNameChanged(string value)
    {
        // M6 配方切换记录时间用于 UI 显示"X 分钟前切换"
        LastRecipeChangeAt = DateTime.Now;
        OnPropertyChanged(nameof(RecipeChangeHint));
        if (this is Shell.MainWindowViewModel shell)
        {
            shell.Recipe.RefreshActiveRecipeParameters();
        }
    }

    // M6 工单切换记录时间（CurrentOrderText 是计算属性，无 OnXxxChanged，
    // 在 RefreshTagsAsync / 写入 tag 时触发：留 hook，主流程后续接入）
    public void NotifyOrderChanged()
    {
        LastOrderChangeAt = DateTime.Now;
        OnPropertyChanged(nameof(OrderChangeHint));
        OnPropertyChanged(nameof(CurrentOrderText));
    }

    partial void OnRefreshIntervalMsChanged(int value)
    {
        if (value <= 100)
        {
            RefreshIntervalMs = 100;
            return;
        }
        _subscriptionTimer.Interval = TimeSpan.FromMilliseconds(RefreshIntervalMs);
        _ = UpdateAutoRefreshStateAsync();
    }

    partial void OnSelectedDesignerPageChanged(DesignerPage? value)
    {
        if (value is null) return;
        LoadPageToCanvas(value);
    }

    [RelayCommand]
    private void SwitchLanguage(string? culture)
    {
        if (string.IsNullOrEmpty(culture)) return;
        var svc = Converters.LocExtension.LocalizationService;
        if (svc is null) return;
        try
        {
            svc.CurrentCulture = culture;
            OnPropertyChanged(nameof(CurrentCulture));
            SystemMessage = $"已切换语言：{culture}";
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "切换语言失败：{Culture}", culture);
            SystemMessage = $"切换语言失败：{culture}";
        }
    }

    /// <summary>当前语言代码（如 "zh-CN" / "en-US"），由 LocalizationService 提供。</summary>
    public string CurrentCulture =>
        Converters.LocExtension.LocalizationService?.CurrentCulture ?? "zh-CN";

    [RelayCommand]
    private void ZoomIn() => UiScale = Math.Min(1.5, Math.Round(UiScale + 0.1, 1));

    [RelayCommand]
    private void ZoomOut() => UiScale = Math.Max(0.7, Math.Round(UiScale - 0.1, 1));

    [RelayCommand]
    private void ResetZoom() => UiScale = 1.0;

    [RelayCommand]
    private void Login(string? roleName)
    {
        if (string.IsNullOrEmpty(roleName))
        {
            SystemMessage = "登录失败：未选择角色";
            return;
        }

        var account = _userService.AuthenticateByRole(roleName, LoginPassword ?? string.Empty);
        if (account is null)
        {
            SystemMessage = "登录失败：密码错误或账号锁定";
            AddLog("登录", SystemMessage, "Warning");
            AddAudit("登录", roleName, "失败", "密码错误或账号锁定");
            return;
        }

        CurrentUserRole = account.Role;
        LoginPassword = string.Empty;
        SystemMessage = $"已登录为：{CurrentRoleText}";
        AddLog("登录", SystemMessage, "Info");
        AddAudit("登录", CurrentRoleText, "成功", SystemMessage);
    }

    [RelayCommand]
    private void Logout()
    {
        CurrentUserRole = UserRole.Operator;
        LoginPassword = string.Empty;
        SystemMessage = "已退出到操作员权限";
        AddLog("登录", SystemMessage, "Info");
        AddAudit("登录", "操作员", "退出", SystemMessage);
    }

    [RelayCommand]
    private void Navigate(string? section)
    {
        if (string.IsNullOrWhiteSpace(section))
        {
            return;
        }

        var target = section.Trim();

        // G7 全局未保存提示：切走当前页前确认
        if (!string.Equals(target, CurrentSection, StringComparison.Ordinal) && IsAnyDirty)
        {
            var result = System.Windows.MessageBox.Show(
                "当前页面有未保存的改动，是否继续切换？\n\n点击「是」放弃改动并切换；点击「否」留在当前页面。",
                "未保存提示",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);
            if (result != System.Windows.MessageBoxResult.Yes)
            {
                return;
            }
        }

        // 任何走 Navigate 的固定段切换都先清掉用户页跳转留下的导航组覆盖。
        // 用户画布页跳转走 NavigateNavItem 后会再次设置 override。
        SetNavigationGroupOverride(null);

        switch (target)
        {
            case "监控":
            case "监视画面":
            case "输入输出监控":
            case "程序监控":
            case "通讯状态监控":
            case "详细生产数据":
                CurrentMonitorSubSection = target switch
                {
                    "监控" => "输入输出监控",
                    "监视画面" => "输入输出监控",
                    _ => target
                };
                CurrentSection = CurrentMonitorSubSection;
                break;
            case "手动操作":
            case "手动画面":
            case "气缸":
            case "轴":
            case "机械手":
            case "电机":
            case "挡停":
                CurrentManualSubSection = target switch
                {
                    "手动操作" => "气缸",
                    "手动画面" => "气缸",
                    _ => target
                };
                CurrentSection = CurrentManualSubSection;
                break;
            case "参数设定":
            case "系统参数设定":
            case "轴参数设定":
            case "气缸参数设定":
            case "真空参数设定":
            case "传感器参数设定":
                CurrentParameterSubSection = target == "参数设定" ? "系统参数设定" : target;
                CurrentSection = CurrentParameterSubSection;
                break;
            case "报警画面":
            case "当前报警":
            case "历史报警":
            case "日志":
            case "报警统计":
                CurrentAlarmSubSection = target == "报警画面" ? "当前报警" : target;
                CurrentSection = CurrentAlarmSubSection;
                break;
            case "设计器":
            case "手动程序生成":
            case "自动程序生成":
            case "初始化程序生成":
                CurrentDesignerSubSection = target == "设计器" ? "手动程序生成" : target;
                CurrentSection = CurrentDesignerSubSection;
                break;
            case "画布设计":
            case "运行页面":
            case "生产计数":
                CurrentSection = target;
                break;
            default:
                CurrentSection = target;
                break;
        }

        SystemMessage = $"已切换到：{CurrentSection}";
    }

    private void BuildNavigation()
    {
        NavigationItems.Add(new NavigationItemViewModel("主界面"));
        NavigationItems.Add(new NavigationItemViewModel("监控", "输入输出监控", "程序监控", "通讯状态监控", "详细生产数据", "生产计数"));
        NavigationItems.Add(new NavigationItemViewModel("手动操作", "气缸", "轴", "机械手", "电机", "挡停"));
        NavigationItems.Add(new NavigationItemViewModel("参数设定", "系统参数设定", "轴参数设定", "气缸参数设定", "真空参数设定", "传感器参数设定"));
        NavigationItems.Add(new NavigationItemViewModel("报警画面", "当前报警", "历史报警", "日志", "报警统计"));
        NavigationItems.Add(new NavigationItemViewModel("登录"));
        // 取消"运行页面"入口：发布后到对应固定画面（如 主界面/手动操作）即可直接看到运行效果
        NavigationItems.Add(new NavigationItemViewModel("设计器", "画布设计", "手动程序生成", "自动程序生成", "初始化程序生成"));
    }

    private static int ResolveTabIndex(string? section)
    {
        if (string.IsNullOrWhiteSpace(section))
        {
            return 0;
        }

        return section.Trim() switch
        {
            "主界面" or "运行总览" => 0,
            "监控" or "监视画面" or "输入输出监控" or "程序监控" or "通讯状态监控" or "详细生产数据" => 1,
            "配方管理" => 2,
            "手动操作" or "手动画面" or "气缸" or "轴" or "机械手" or "电机" or "挡停" => 3,
            "参数设定" or "系统参数设定" or "轴参数设定" or "气缸参数设定" or "真空参数设定" or "传感器参数设定" => 4,
            "报警画面" or "当前报警" or "历史报警" or "日志" or "报警统计" => 5,
            "登录" or "登录权限" => 6,
            "操作审计" => 7,
            "设计器" or "手动程序生成" or "自动程序生成" or "初始化程序生成" => 8,
            "画布设计" => 9,
            "运行页面" => 10,
            "生产计数" => 11,
            _ => 0
        };
    }

    private static string GetSectionNameByTabIndex(int tabIndex) => tabIndex switch
    {
        0 => "运行总览",
        1 => "监视画面",
        2 => "配方管理",
        3 => "手动画面",
        4 => "参数设定",
        5 => "报警画面",
        6 => "登录权限",
        7 => "操作审计",
        8 => "设计器",
        9 => "画布设计",
        10 => "运行页面",
        _ => "运行总览"
    };

    private static string ResolveNavigationGroup(string? section)
    {
        return section switch
        {
            "主界面" or "运行总览" => "主界面",
            "监控" or "监视画面" or "输入输出监控" or "程序监控" or "通讯状态监控" or "详细生产数据" or "生产计数" => "监控",
            "配方管理" => "配方管理",
            "手动操作" or "手动画面" or "气缸" or "轴" or "机械手" or "电机" or "挡停" => "手动操作",
            "参数设定" or "系统参数设定" or "轴参数设定" or "气缸参数设定" or "真空参数设定" or "传感器参数设定" => "参数设定",
            "报警画面" or "当前报警" or "历史报警" or "日志" or "报警统计" => "报警画面",
            "登录" or "登录权限" => "登录",
            "操作审计" => "操作审计",
            "设计器" or "画布设计" or "运行页面" or "手动程序生成" or "自动程序生成" or "初始化程序生成" => "设计器",
            _ => string.Empty
        };
    }

    private void SeedDemoData()
    {
        AddTag(new TagItem { Name = "X_Start", NodeId = "ns=2;s=Channel1.Device1.X_Start", DataType = "Boolean", Category = "IO", Group = "Input", Direction = "Input", CurrentValue = "False", IsWritable = false });
        AddTag(new TagItem { Name = "Y_RunLamp", NodeId = "ns=2;s=Channel1.Device1.Y_RunLamp", DataType = "Boolean", Category = "IO", Group = "Output", Direction = "Output", CurrentValue = "False", IsWritable = true });
        AddTag(new TagItem { Name = "Axis1_Pos", NodeId = "ns=2;s=Channel1.Device1.Axis1_Pos", DataType = "Double", Category = "Axis", Group = "Motion", Direction = "Output", CurrentValue = "0.0", IsWritable = true });
        AddTag(new TagItem { Name = "Alarm_EStop", NodeId = "ns=2;s=Channel1.Device1.Alarm_EStop", DataType = "Boolean", Category = "Alarm", Group = "Alarm", Direction = "Input", CurrentValue = "False", IsAlarm = true, IsWritable = false });
        AddTag(new TagItem { Name = "Cylinder_Extend", NodeId = "ns=2;s=Channel1.Device1.Cylinder_Extend", DataType = "Boolean", Category = "Cylinder", Group = "Actuator", Direction = "Output", CurrentValue = "False", IsWritable = true });
        AddTag(new TagItem { Name = "Stopper_Up", NodeId = "ns=2;s=Channel1.Device1.Stopper_Up", DataType = "Boolean", Category = "Stopper", Group = "Actuator", Direction = "Output", CurrentValue = "False", IsWritable = true });
        AddTag(new TagItem { Name = "Robot_Run", NodeId = "ns=2;s=Channel1.Device1.Robot_Run", DataType = "Boolean", Category = "Robot", Group = "Actuator", Direction = "Output", CurrentValue = "False", IsWritable = true });
        AddTag(new TagItem { Name = "Motor1_Fault", NodeId = "ns=2;s=Channel1.Device1.Motor1_Fault", DataType = "Boolean", Category = "Alarm", Group = "Motor", Direction = "Input", CurrentValue = "False", IsAlarm = true, IsWritable = false });
        AddTag(new TagItem { Name = "Motor1_Reset", NodeId = "ns=2;s=Channel1.Device1.Motor1_Reset", DataType = "Boolean", Category = "Motor", Group = "Command", Direction = "Output", CurrentValue = "False", IsWritable = true });
        AddTag(new TagItem { Name = "Cylinder_FwdLS", NodeId = "ns=2;s=Channel1.Device1.Cylinder_FwdLS", DataType = "Boolean", Category = "Cylinder", Group = "Status", Direction = "Input", CurrentValue = "False", IsWritable = false });
        AddTag(new TagItem { Name = "Cylinder_BwdLS", NodeId = "ns=2;s=Channel1.Device1.Cylinder_BwdLS", DataType = "Boolean", Category = "Cylinder", Group = "Status", Direction = "Input", CurrentValue = "True", IsWritable = false });
        AddTag(new TagItem { Name = "Axis1_Enable", NodeId = "ns=2;s=Channel1.Device1.Axis1_Enable", DataType = "Boolean", Category = "Axis", Group = "Command", Direction = "Output", CurrentValue = "False", IsWritable = true });
        AddTag(new TagItem { Name = "Axis1_Alarm", NodeId = "ns=2;s=Channel1.Device1.Axis1_Alarm", DataType = "Boolean", Category = "Alarm", Group = "Axis", Direction = "Input", CurrentValue = "False", IsAlarm = true, IsWritable = false });
        AddTag(new TagItem { Name = "Axis1_AlarmReset", NodeId = "ns=2;s=Channel1.Device1.Axis1_AlarmReset", DataType = "Boolean", Category = "Axis", Group = "Command", Direction = "Output", CurrentValue = "False", IsWritable = true });
        AddTag(new TagItem { Name = "Robot_Pause", NodeId = "ns=2;s=Channel1.Device1.Robot_Pause", DataType = "Boolean", Category = "Robot", Group = "Command", Direction = "Output", CurrentValue = "False", IsWritable = true });
        AddTag(new TagItem { Name = "Robot_Reset", NodeId = "ns=2;s=Channel1.Device1.Robot_Reset", DataType = "Boolean", Category = "Robot", Group = "Command", Direction = "Output", CurrentValue = "False", IsWritable = true });
        AddTag(new TagItem { Name = "Mode_Debug", NodeId = "ns=2;s=Channel1.Device1.Mode_Debug", DataType = "Boolean", Category = "Mode", Group = "RunMode", Direction = "Output", CurrentValue = "False", IsWritable = true });
        AddTag(new TagItem { Name = "Mode_DryRun", NodeId = "ns=2;s=Channel1.Device1.Mode_DryRun", DataType = "Boolean", Category = "Mode", Group = "RunMode", Direction = "Output", CurrentValue = "False", IsWritable = true });
        AddTag(new TagItem { Name = "Mode_BypassStation", NodeId = "ns=2;s=Channel1.Device1.Mode_BypassStation", DataType = "Boolean", Category = "Mode", Group = "RunMode", Direction = "Output", CurrentValue = "False", IsWritable = true });
        AddTag(new TagItem { Name = "Mode_Manual", NodeId = "ns=2;s=Channel1.Device1.Mode_Manual", DataType = "Boolean", Category = "Mode", Group = "RunMode", Direction = "Output", CurrentValue = "True", IsWritable = true });
        AddTag(new TagItem { Name = "Mode_Auto", NodeId = "ns=2;s=Channel1.Device1.Mode_Auto", DataType = "Boolean", Category = "Mode", Group = "RunMode", Direction = "Output", CurrentValue = "False", IsWritable = true });
        AddTag(new TagItem { Name = "Device_Start", NodeId = "ns=2;s=Channel1.Device1.Device_Start", DataType = "Boolean", Category = "System", Group = "RunControl", Direction = "Output", CurrentValue = "False", IsWritable = true });
        AddTag(new TagItem { Name = "Production_Count", NodeId = "ns=2;s=Channel1.Device1.Production_Count", DataType = "Int32", Category = "Production", Group = "Summary", Direction = "Input", CurrentValue = "1280", IsWritable = false });
        AddTag(new TagItem { Name = "Production_GoodCount", NodeId = "ns=2;s=Channel1.Device1.Production_GoodCount", DataType = "Int32", Category = "Production", Group = "Summary", Direction = "Input", CurrentValue = "1246", IsWritable = false });
        AddTag(new TagItem { Name = "Production_NgCount", NodeId = "ns=2;s=Channel1.Device1.Production_NgCount", DataType = "Int32", Category = "Production", Group = "Summary", Direction = "Input", CurrentValue = "34", IsWritable = false });
        AddTag(new TagItem { Name = "Shift_ProductionCount", NodeId = "ns=2;s=Channel1.Device1.Shift_ProductionCount", DataType = "Int32", Category = "Production", Group = "Shift", Direction = "Input", CurrentValue = "460", IsWritable = false });
        AddTag(new TagItem { Name = "Shift_GoodCount", NodeId = "ns=2;s=Channel1.Device1.Shift_GoodCount", DataType = "Int32", Category = "Production", Group = "Shift", Direction = "Input", CurrentValue = "450", IsWritable = false });
        AddTag(new TagItem { Name = "Shift_NgCount", NodeId = "ns=2;s=Channel1.Device1.Shift_NgCount", DataType = "Int32", Category = "Production", Group = "Shift", Direction = "Input", CurrentValue = "10", IsWritable = false });
        AddTag(new TagItem { Name = "Daily_ProductionCount", NodeId = "ns=2;s=Channel1.Device1.Daily_ProductionCount", DataType = "Int32", Category = "Production", Group = "Daily", Direction = "Input", CurrentValue = "1280", IsWritable = false });
        AddTag(new TagItem { Name = "Daily_GoodCount", NodeId = "ns=2;s=Channel1.Device1.Daily_GoodCount", DataType = "Int32", Category = "Production", Group = "Daily", Direction = "Input", CurrentValue = "1246", IsWritable = false });
        AddTag(new TagItem { Name = "Daily_NgCount", NodeId = "ns=2;s=Channel1.Device1.Daily_NgCount", DataType = "Int32", Category = "Production", Group = "Daily", Direction = "Input", CurrentValue = "34", IsWritable = false });
        AddTag(new TagItem { Name = "Production_TargetCount", NodeId = "ns=2;s=Channel1.Device1.Production_TargetCount", DataType = "Int32", Category = "Production", Group = "Summary", Direction = "Input", CurrentValue = "1500", IsWritable = false });
        AddTag(new TagItem { Name = "Production_Availability", NodeId = "ns=2;s=Channel1.Device1.Production_Availability", DataType = "Double", Category = "Production", Group = "OEE", Direction = "Input", CurrentValue = "92.5", IsWritable = false });
        AddTag(new TagItem { Name = "Production_Performance", NodeId = "ns=2;s=Channel1.Device1.Production_Performance", DataType = "Double", Category = "Production", Group = "OEE", Direction = "Input", CurrentValue = "88.2", IsWritable = false });
        AddTag(new TagItem { Name = "Production_Quality", NodeId = "ns=2;s=Channel1.Device1.Production_Quality", DataType = "Double", Category = "Production", Group = "OEE", Direction = "Input", CurrentValue = "97.3", IsWritable = false });
        AddTag(new TagItem { Name = "Cycle_Time", NodeId = "ns=2;s=Channel1.Device1.Cycle_Time", DataType = "Double", Category = "Production", Group = "Detail", Direction = "Input", CurrentValue = "3.2", IsWritable = false });
        AddTag(new TagItem { Name = "Ideal_Cycle_Time", NodeId = "ns=2;s=Channel1.Device1.Ideal_Cycle_Time", DataType = "Double", Category = "Production", Group = "Detail", Direction = "Input", CurrentValue = "2.8", IsWritable = false });
        AddTag(new TagItem { Name = "Throughput_Hourly", NodeId = "ns=2;s=Channel1.Device1.Throughput_Hourly", DataType = "Int32", Category = "Production", Group = "Detail", Direction = "Input", CurrentValue = "380", IsWritable = false });
        AddTag(new TagItem { Name = "WorkOrder_No", NodeId = "ns=2;s=Channel1.Device1.WorkOrder_No", DataType = "String", Category = "Production", Group = "Order", Direction = "Input", CurrentValue = "WO-20260404-01", IsWritable = false });
        AddTag(new TagItem { Name = "Recipe_Name", NodeId = "ns=2;s=Channel1.Device1.Recipe_Name", DataType = "String", Category = "Production", Group = "Order", Direction = "Input", CurrentValue = "产品A", IsWritable = false });
        AddTag(new TagItem { Name = "Machine_RunTimeMin", NodeId = "ns=2;s=Channel1.Device1.Machine_RunTimeMin", DataType = "Int32", Category = "Production", Group = "Detail", Direction = "Input", CurrentValue = "420", IsWritable = false });
        AddTag(new TagItem { Name = "Machine_StopTimeMin", NodeId = "ns=2;s=Channel1.Device1.Machine_StopTimeMin", DataType = "Int32", Category = "Production", Group = "Detail", Direction = "Input", CurrentValue = "34", IsWritable = false });
        AddTag(new TagItem { Name = "Alarm_AirLow", NodeId = "ns=2;s=Channel1.Device1.Alarm_AirLow", DataType = "Boolean", Category = "Alarm", Group = "Utility", Direction = "Input", CurrentValue = "True", IsAlarm = true, IsWritable = false });
        AddTag(new TagItem { Name = "Alarm_ServoOverload", NodeId = "ns=2;s=Channel1.Device1.Alarm_ServoOverload", DataType = "Boolean", Category = "Alarm", Group = "Axis", Direction = "Input", CurrentValue = "False", IsAlarm = true, IsWritable = false });
        AddTag(new TagItem { Name = "Alarm_VacuumTimeout", NodeId = "ns=2;s=Channel1.Device1.Alarm_VacuumTimeout", DataType = "Boolean", Category = "Alarm", Group = "Process", Direction = "Input", CurrentValue = "False", IsAlarm = true, IsWritable = false });

        EventBindings.Add(new EventBinding { TagName = "Alarm_EStop", TriggerCondition = "True", EventName = "急停报警", ActionTarget = "当前报警", ActionParameter = "急停触发", Description = "E-Stop 触发时写入报警" });
        EventBindings.Add(new EventBinding { TagName = "Motor1_Fault", TriggerCondition = "True", EventName = "电机故障", ActionTarget = "当前报警", ActionParameter = "电机1故障", Description = "电机故障报警" });
        EventBindings.Add(new EventBinding { TagName = "Axis1_Alarm", TriggerCondition = "True", EventName = "轴报警", ActionTarget = "当前报警", ActionParameter = "轴报警", Description = "轴故障报警" });
        EventBindings.Add(new EventBinding { TagName = "Alarm_AirLow", TriggerCondition = "True", EventName = "气压报警", ActionTarget = "当前报警", ActionParameter = "气源不足", Description = "气压不足报警" });
        EventBindings.Add(new EventBinding { TagName = "Alarm_ServoOverload", TriggerCondition = "True", EventName = "伺服过载", ActionTarget = "当前报警", ActionParameter = "轴过载", Description = "伺服过载报警" });
        EventBindings.Add(new EventBinding { TagName = "Alarm_VacuumTimeout", TriggerCondition = "True", EventName = "真空超时", ActionTarget = "当前报警", ActionParameter = "真空建立失败", Description = "真空报警" });

        CurrentAlarms.Add(new AlarmRecord { Time = DateTime.Now, Level = "Warning", Source = "Alarm_AirLow", Message = "示例报警：气压低", Active = true, Acknowledged = false, State = "Active", Count = 4 });
        CurrentAlarms.Add(new AlarmRecord { Time = DateTime.Now.AddMinutes(-3), Level = "Alarm", Source = "Motor1_Fault", Message = "电机故障 - 电机1故障", Active = true, Acknowledged = false, State = "Active", Count = 2 });
        _activeAlarmMap["Alarm_AirLow|气源不足"] = CurrentAlarms[0];
        _activeAlarmMap["Motor1_Fault|电机1故障"] = CurrentAlarms[1];
        AlarmHistory.Add(new AlarmRecord { Time = DateTime.Now.AddMinutes(-60), Level = "Error", Source = "Alarm_EStop", Message = "急停触发", Active = false, Acknowledged = true, ClearTime = DateTime.Now.AddMinutes(-58), State = "Cleared", Count = 1 });
        AlarmHistory.Add(new AlarmRecord { Time = DateTime.Now.AddMinutes(-40), Level = "Warning", Source = "Alarm_AirLow", Message = "气压报警 - 气源不足", Active = false, Acknowledged = true, ClearTime = DateTime.Now.AddMinutes(-35), State = "Cleared", Count = 6 });
        AlarmHistory.Add(new AlarmRecord { Time = DateTime.Now.AddMinutes(-25), Level = "Alarm", Source = "Motor1_Fault", Message = "电机故障 - 电机1故障", Active = false, Acknowledged = true, ClearTime = DateTime.Now.AddMinutes(-20), State = "Cleared", Count = 3 });
        AlarmHistory.Add(new AlarmRecord { Time = DateTime.Now.AddMinutes(-15), Level = "Alarm", Source = "Axis1_Alarm", Message = "轴报警 - 轴报警", Active = false, Acknowledged = true, ClearTime = DateTime.Now.AddMinutes(-10), State = "Cleared", Count = 2 });
        Logs.Add(new AlarmRecord { Time = DateTime.Now, Level = "Info", Source = "系统", Message = "程序启动", Active = false, Acknowledged = true, State = "Logged", Count = 1 });
        Logs.Add(new AlarmRecord { Time = DateTime.Now.AddMinutes(-2), Level = "Info", Source = "生产", Message = "当前班次累计 460 件，目标 1500 件", Active = false, Acknowledged = true, State = "Logged", Count = 1 });
        OnPropertyChanged(nameof(TagCount));
        OnPropertyChanged(nameof(AlarmCount));
    }

    public void UpdateRuntimeVisuals()
    {
        RefreshManualCylinderBlockStates();
        RefreshManualAxisBlockStates();
        RefreshCylinderBindingProperties();
        RefreshAxisBindingProperties();
        foreach (var element in DesignerElements)
        {
            var tag = Tags.FirstOrDefault(t => t.Name.Equals(element.TagBinding, StringComparison.OrdinalIgnoreCase));
            if (tag is null) continue;
            switch (element.ElementType)
            {
                case "Indicator": element.Background = tag.CurrentValue.Equals("True", StringComparison.OrdinalIgnoreCase) ? "#22C55E" : "#475569"; break;
                case "ValueDisplay": element.Text = $"{tag.Name}:{tag.CurrentValue}"; break;
                case "AlarmBanner": element.Background = ActiveAlarmCount > 0 ? "#DC2626" : "#0F766E"; element.Text = ActiveAlarmCount > 0 ? $"报警中 {ActiveAlarmCount} 条" : "系统无活动报警"; break;
                case "Motor":
                    var motorRunning = GetBoolTag("Y_RunLamp");
                    var motorFault = GetBoolTag("Motor1_Fault");
                    element.Background = motorFault ? "#B91C1C" : motorRunning ? "#2563EB" : "#475569";
                    element.Text = motorFault ? "电机故障" : motorRunning ? "电机运行" : "电机停止";
                    break;
                case "Cylinder":
                    var fwd = GetBoolTag("Cylinder_FwdLS");
                    var bwd = GetBoolTag("Cylinder_BwdLS");
                    element.Background = fwd ? "#6987b0" : bwd ? "#334155" : "#D97706";
                    element.Text = fwd ? "前到位" : bwd ? "后到位" : "切换中";
                    break;
                case "Axis":
                    var axisEnable = GetBoolTag("Axis1_Enable");
                    var axisAlarm = GetBoolTag("Axis1_Alarm");
                    element.Background = axisAlarm ? "#B91C1C" : axisEnable ? "#7C3AED" : "#475569";
                    element.Text = axisAlarm ? $"轴报警 Pos:{GetTagValue("Axis1_Pos")}" : axisEnable ? $"轴使能 Pos:{GetTagValue("Axis1_Pos")}" : $"轴未使能 Pos:{GetTagValue("Axis1_Pos")}";
                    break;
                case "Robot":
                    var robotRun = GetBoolTag("Robot_Run");
                    var robotPause = GetBoolTag("Robot_Pause");
                    element.Background = robotPause ? "#F59E0B" : robotRun ? "#DB2777" : "#475569";
                    element.Text = robotPause ? "机械手暂停" : robotRun ? "机械手运行" : "机械手待机";
                    break;
                case "Stopper":
                    element.Background = tag.CurrentValue.Equals("True", StringComparison.OrdinalIgnoreCase) ? "#D97706" : "#475569";
                    element.Text = tag.CurrentValue.Equals("True", StringComparison.OrdinalIgnoreCase) ? "挡停升起" : "挡停下降";
                    break;
            }
        }

        OnPropertyChanged(nameof(ActiveAlarmCount));
        OnPropertyChanged(nameof(UnacknowledgedAlarmCount));
        OnPropertyChanged(nameof(ProductionCount));
        OnPropertyChanged(nameof(GoodCount));
        OnPropertyChanged(nameof(NgCount));
        OnPropertyChanged(nameof(ShiftProductionCount));
        OnPropertyChanged(nameof(ShiftGoodCount));
        OnPropertyChanged(nameof(ShiftNgCount));
        OnPropertyChanged(nameof(DailyProductionCount));
        OnPropertyChanged(nameof(DailyGoodCount));
        OnPropertyChanged(nameof(DailyNgCount));
        OnPropertyChanged(nameof(TargetCount));
        OnPropertyChanged(nameof(AvailabilityRate));
        OnPropertyChanged(nameof(PerformanceRate));
        OnPropertyChanged(nameof(QualityRate));
        OnPropertyChanged(nameof(OeeRate));
        OnPropertyChanged(nameof(DeviceStatusText));
        OnPropertyChanged(nameof(ShiftStatusText));
        OnPropertyChanged(nameof(CurrentRecipeText));
        OnPropertyChanged(nameof(CurrentOrderText));
        OnPropertyChanged(nameof(MotorStatusText));
        OnPropertyChanged(nameof(CylinderStatusText));
        OnPropertyChanged(nameof(CylinderForwardActive));
        OnPropertyChanged(nameof(CylinderBackwardActive));
        OnPropertyChanged(nameof(CylinderOutputActive));
        OnPropertyChanged(nameof(CylinderOutputText));
        OnPropertyChanged(nameof(CylinderCurrentStateText));
        OnPropertyChanged(nameof(CylinderDisplayName));
        OnPropertyChanged(nameof(CylinderActionHint));
        OnPropertyChanged(nameof(CylinderInterlockBaseReady));
        OnPropertyChanged(nameof(CylinderHomeInterlockActive));
        OnPropertyChanged(nameof(CylinderMoveInterlockActive));
        OnPropertyChanged(nameof(CylinderHomeInterlockText));
        OnPropertyChanged(nameof(CylinderMoveInterlockText));
        OnPropertyChanged(nameof(CylinderInterlockHint));
        OnPropertyChanged(nameof(AxisStatusText));
        OnPropertyChanged(nameof(RobotStatusText));
        OnPropertyChanged(nameof(IsDebugMode));
        OnPropertyChanged(nameof(IsDryRunMode));
        OnPropertyChanged(nameof(IsBypassStationMode));
        OnPropertyChanged(nameof(IsManualMode));
        OnPropertyChanged(nameof(IsAutoMode));
        OnPropertyChanged(nameof(RunModeSummary));
        OnPropertyChanged(nameof(StartStopSummary));
        OnPropertyChanged(nameof(StartModeReady));
        OnPropertyChanged(nameof(StartAlarmReady));
        OnPropertyChanged(nameof(StartInterlockReady));
        OnPropertyChanged(nameof(ProductionTrendSummary));
        OnPropertyChanged(nameof(OeeTrendSummary));
        OnPropertyChanged(nameof(AlarmTrendSummary));
        OnPropertyChanged(nameof(FocusAlarmHint));
    }

    public bool CanEditParameter(ParameterItem parameter) => CurrentUserRole >= parameter.MinRole;
    private bool GetBoolTag(string tagName) => TryParseTagBool(GetTagValue(tagName), out var value) && value;
    private bool GetCylinderBool(string configuredTagName, string primarySuffix = "", string? secondarySuffix = null, string fallbackTagName = "", bool fallbackValue = false)
    {
        if (!string.IsNullOrWhiteSpace(configuredTagName))
        {
            var direct = GetTagValue(configuredTagName);
            if (!string.IsNullOrWhiteSpace(direct)
                && !string.Equals(direct, "--", StringComparison.Ordinal)
                && TryParseTagBool(direct, out var configuredValue))
            {
                return configuredValue;
            }
        }

        var primaryValue = GetImportedCylinderTagValue(primarySuffix);
        if (!string.IsNullOrWhiteSpace(primaryValue) && !string.Equals(primaryValue, "--", StringComparison.Ordinal))
        {
            return TryParseTagBool(primaryValue, out var primaryValueBool) && primaryValueBool;
        }

        if (!string.IsNullOrWhiteSpace(secondarySuffix))
        {
            var secondaryValue = GetImportedCylinderTagValue(secondarySuffix);
            if (!string.IsNullOrWhiteSpace(secondaryValue) && !string.Equals(secondaryValue, "--", StringComparison.Ordinal))
            {
                return TryParseTagBool(secondaryValue, out var secondaryValueBool) && secondaryValueBool;
            }
        }

        if (!string.IsNullOrWhiteSpace(fallbackTagName))
        {
            return GetBoolTag(fallbackTagName);
        }

        return fallbackValue;
    }

    private static bool TryParseTagBool(string? raw, out bool value)
    {
        value = false;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var normalized = raw.Trim();
        if (bool.TryParse(normalized, out value))
        {
            return true;
        }

        switch (normalized)
        {
            case "1":
            case "ON":
            case "On":
            case "on":
                value = true;
                return true;
            case "0":
            case "OFF":
            case "Off":
            case "off":
                value = false;
                return true;
            default:
                return false;
        }
    }

    private bool GetBoolParameter(string parameterName, bool fallback = false)
    {
        var item = Parameters.FirstOrDefault(p => p.Name.Equals(parameterName, StringComparison.OrdinalIgnoreCase));
        return item is null ? fallback : bool.TryParse(item.Value, out var result) ? result : fallback;
    }
    private int GetIntTag(string tagName, int fallback = 0) => int.TryParse(GetTagValue(tagName), out var value) ? value : fallback;
    private double GetDoubleTag(string tagName, double fallback = 0) => double.TryParse(GetTagValue(tagName), out var value) ? value : fallback;

    private string GetTagValue(string tagNameOrKey)
    {
        if (string.IsNullOrWhiteSpace(tagNameOrKey))
        {
            return "--";
        }

        var key = tagNameOrKey.Trim();
        var tag = FindTagByNameOrNodeId(key);
        if (tag is not null
            && !string.IsNullOrWhiteSpace(tag.CurrentValue)
            && !string.Equals(tag.CurrentValue, "--", StringComparison.Ordinal)
            && !tag.CurrentValue.StartsWith("ERR:", StringComparison.OrdinalIgnoreCase))
        {
            return tag.CurrentValue;
        }

        if (_opcStringValueByBindingKey.TryGetValue(key, out var live)
            && !string.IsNullOrWhiteSpace(live)
            && !string.Equals(live, "--", StringComparison.Ordinal))
        {
            return live;
        }

        if (tag is not null)
        {
            return string.IsNullOrWhiteSpace(tag.CurrentValue) ? "--" : tag.CurrentValue;
        }

        return "--";
    }

    public void SetTagValue(string tagName, string value)
    {
        var tag = FindTagByNameOrNodeId(tagName);
        if (tag is not null)
        {
            tag.CurrentValue = value;
        }

        if (!string.IsNullOrWhiteSpace(tagName))
        {
            _opcStringValueByBindingKey[tagName.Trim()] = value;
        }
    }

    internal void RecordOpcBindingString(string? bindingKey, string? value)
    {
        if (string.IsNullOrWhiteSpace(bindingKey) || value is null)
        {
            return;
        }

        _opcStringValueByBindingKey[bindingKey.Trim()] = value;
    }

    private void OnPlcReadAppliedToTag(TagItem tag, string value)
    {
        tag.CurrentValue = value;
        if (!string.IsNullOrWhiteSpace(tag.Name))
        {
            _opcStringValueByBindingKey[tag.Name] = value;
        }

        if (!string.IsNullOrWhiteSpace(tag.NodeId))
        {
            _opcStringValueByBindingKey[tag.NodeId] = value;
        }
    }

    private void ClearOpcBindingValueCache()
    {
        _opcStringValueByBindingKey.Clear();
    }
    private TagItem? FindTagByNameOrNodeId(string tagNameOrNodeId)
    {
        if (string.IsNullOrWhiteSpace(tagNameOrNodeId))
        {
            return null;
        }

        if (LooksLikeNodeIdOrPath(tagNameOrNodeId))
        {
            return FindTagByNodeId(tagNameOrNodeId)
                ?? FindTagByName(tagNameOrNodeId);
        }

        return FindTagByName(tagNameOrNodeId)
            ?? FindTagByNodeId(tagNameOrNodeId);
    }

    private TagItem? FindTagByName(string tagName) =>
        Tags.FirstOrDefault(t => t.Name.Equals(tagName, StringComparison.OrdinalIgnoreCase));

    private TagItem? FindTagByNodeId(string nodeId) =>
        Tags.FirstOrDefault(t => t.NodeId.Equals(nodeId, StringComparison.OrdinalIgnoreCase));

    private static bool LooksLikeNodeIdOrPath(string value) =>
        value.StartsWith("ns=", StringComparison.OrdinalIgnoreCase)
        || value.Contains('.', StringComparison.Ordinal)
        || value.Contains('[', StringComparison.Ordinal)
        || value.Contains(']', StringComparison.Ordinal);
    private string GetImportedCylinderTagValue(string suffix) => FindImportedCylinderTag(suffix)?.CurrentValue ?? "--";

    private TagItem? FindBestImportedCylinderCommandTag(ManualCylinderBlockItem? block, string suffix)
    {
        var blockIndex = block?.CylinderIndex ?? 0;
        var candidates = Tags.Where(tag => !string.IsNullOrWhiteSpace(tag.NodeId)
            && tag.NodeId.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
            && !IsGeneratedCylinderPlaceholder(tag));

        if (blockIndex > 0)
        {
            // 精确匹配当前气缸索引，不再 fallback 到第一个气缸
            return candidates.FirstOrDefault(tag => tag.NodeId.Contains($".CylCtrl[{blockIndex}]", StringComparison.OrdinalIgnoreCase));
        }

        return candidates.FirstOrDefault() ?? FindImportedCylinderTag(suffix);
    }

    private static bool IsGeneratedCylinderPlaceholder(TagItem? tag)
    {
        if (tag is null)
        {
            return false;
        }

        return string.Equals(tag.Group, "Imported", StringComparison.OrdinalIgnoreCase)
            && tag.Description.Contains("自动补齐", StringComparison.OrdinalIgnoreCase);
    }

    private TagItem? FindImportedCylinderTag(string suffix)
    {
        var prefix = ResolveImportedCylinderPrefix();
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return null;
        }

        var fullNodeId = prefix + suffix;
        return Tags.FirstOrDefault(tag => tag.NodeId.Equals(fullNodeId, StringComparison.OrdinalIgnoreCase))
            ?? Tags.FirstOrDefault(tag => tag.Name.Equals(fullNodeId.Replace("Application.", string.Empty, StringComparison.OrdinalIgnoreCase), StringComparison.OrdinalIgnoreCase));
    }

    private string ResolveImportedCylinderPrefix()
    {
        var inHomeTag = Tags.FirstOrDefault(tag =>
            tag.NodeId.Contains(".CylCtrl[", StringComparison.OrdinalIgnoreCase)
            && tag.NodeId.EndsWith(".Status.InHome", StringComparison.OrdinalIgnoreCase));

        if (inHomeTag is not null)
        {
            return inHomeTag.NodeId[..^".Status.InHome".Length];
        }

        var devStatusTag = Tags.FirstOrDefault(tag =>
            tag.NodeId.Contains(".CylCtrl[", StringComparison.OrdinalIgnoreCase)
            && tag.NodeId.EndsWith(".DevStatus.Sensor_Home", StringComparison.OrdinalIgnoreCase));

        return devStatusTag is null ? string.Empty : devStatusTag.NodeId[..^".DevStatus.Sensor_Home".Length];
    }

    private string GetImportedCylinderDisplayName()
    {
        var prefix = ResolveImportedCylinderPrefix();
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return "夹紧气缸 CY01";
        }

        var commentTag = FindImportedCylinderTag(".Parm.Comment");
        if (commentTag is not null && !string.IsNullOrWhiteSpace(commentTag.CurrentValue) && !string.Equals(commentTag.CurrentValue, "--", StringComparison.Ordinal))
        {
            return commentTag.CurrentValue;
        }

        var indexMarker = prefix.LastIndexOf('[');
        var normalized = prefix.Replace("Application.", string.Empty, StringComparison.OrdinalIgnoreCase);
        return indexMarker >= 0 ? $"气缸 {normalized[indexMarker..]}" : normalized;
    }

    private void AddTag(TagItem tag) => Tags.Add(tag);
    private double CalculateAvailability() { var run = GetDoubleTag("Machine_RunTimeMin", 420); var stop = GetDoubleTag("Machine_StopTimeMin", 34); var total = run + stop; return total <= 0 ? 0 : Math.Round(run / total * 100, 1); }
    private double CalculatePerformance() { var ideal = GetDoubleTag("Ideal_Cycle_Time", 2.8); var actual = GetDoubleTag("Cycle_Time", 3.2); if (ideal <= 0 || actual <= 0) return 0; return Math.Round(Math.Min(100, ideal / actual * 100), 1); }
    private double CalculateQuality() { var total = ProductionCount; return total <= 0 ? 0 : Math.Round((double)GoodCount / total * 100, 1); }
    private static string BuildSparklinePath(IEnumerable<double> values)
    {
        var points = values.ToList();
        if (points.Count == 0) return "";
        var max = points.Max();
        var min = points.Min();
        var range = Math.Max(1, max - min);
        var stepX = points.Count == 1 ? 100 : 100.0 / (points.Count - 1);
        var coordinates = points.Select((v, i) =>
        {
            var x = i * stepX;
            var y = 40 - ((v - min) / range * 32 + 4);
            return $"{x.ToString("F1", CultureInfo.InvariantCulture)},{y.ToString("F1", CultureInfo.InvariantCulture)}";
        }).ToArray();
        return "M " + string.Join(" L ", coordinates);
    }

    public void AddLog(string source, string message, string level)
    {
        if (System.Windows.Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(() => AddLog(source, message, level));
            return;
        }

        Logs.Insert(0, new AlarmRecord { Time = DateTime.Now, Source = source, Message = message, Level = level, Active = false, Acknowledged = true, State = "Logged", Count = 1 });
        OnPropertyChanged(nameof(FocusAlarmHint));
    }

    public void AddAudit(string action, string target, string result, string detail)
    {
        OperationAudits.Insert(0, new OperationAuditRecord
        {
            Time = DateTime.Now,
            User = LoginUser,
            Action = action,
            Target = target,
            Result = result,
            Detail = detail
        });
    }

    public void ShowPopup(string title, string message, string level = "Info")
    {
        SystemMessage = message;
        AddLog("弹窗", $"{title}: {message}", level == "Error" ? "Error" : "Warning");
        AddAudit("弹窗", title, level, message);
        PopupRequested?.Invoke(title, message, level);
    }

    public void UpdateStartHoldProgress(double progress)
    {
        StartHoldProgress = progress;
    }

    public bool RequestConfirmation(string title, string message)
    {
        var result = ConfirmationRequested?.Invoke(title, message) ?? true;
        AddAudit("确认框", title, result ? "确认" : "取消", message);
        return result;
    }

    private static object ConvertValue(string rawValue, string dataType) => dataType.ToLowerInvariant() switch
    {
        "boolean" or "bool" => bool.Parse(rawValue),
        "int16" or "short" => short.Parse(rawValue),
        "int32" or "int" => int.Parse(rawValue),
        "int64" or "long" => long.Parse(rawValue),
        "float" or "single" => float.Parse(rawValue),
        "double" => double.Parse(rawValue),
        _ => rawValue
    };

    private static async Task RunOnUiThreadAsync(Action action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
            return;
        }

        await dispatcher.InvokeAsync(action);
    }

    private string GetApplicationRoot()
    {
        var currentDirectory = Environment.CurrentDirectory;
        if (File.Exists(Path.Combine(currentDirectory, "ApexHMI.csproj")))
        {
            return currentDirectory;
        }

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ApexHMI.csproj")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return AppContext.BaseDirectory;
    }

    public string GetProjectRoot() => GetApplicationRoot();
    private double Snap(double value) => EnableGridSnap ? Math.Round(value / GridSize) * GridSize : value;
}
