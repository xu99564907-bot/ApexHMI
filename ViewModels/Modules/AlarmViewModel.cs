using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ApexHMI.Interfaces;
using ApexHMI.Models;
using CommunityToolkit.Mvvm.Input;

namespace ApexHMI.ViewModels.Modules;

public sealed class AlarmViewModel : ModuleViewModelBase
{
    private readonly IAlarmService _alarmService;

    public AlarmViewModel(MainViewModel shell, IAlarmService alarmService)
        : base(shell, "报警画面")
    {
        _alarmService = alarmService;
        AcknowledgeAllAlarmsCommand = new RelayCommand(AcknowledgeAllAlarms);
        ResetAllAlarmsCommand = new AsyncRelayCommand(ResetAllAlarmsAsync);
        SaveAlarmHistoryCommand = new AsyncRelayCommand(SaveAlarmHistoryAsync);
        LoadAlarmHistoryCommand = new AsyncRelayCommand(LoadAlarmHistoryAsync);
    }

    public IRelayCommand AcknowledgeAllAlarmsCommand { get; }
    public IAsyncRelayCommand ResetAllAlarmsCommand { get; }
    public IAsyncRelayCommand SaveAlarmHistoryCommand { get; }
    public IAsyncRelayCommand LoadAlarmHistoryCommand { get; }
    public string CurrentSubSection => Shell.CurrentAlarmSubSection;
    public string CurrentAlarmTitle => Shell.CurrentAlarmTitle;
    public ObservableCollection<AlarmRecord> CurrentAlarms => Shell.CurrentAlarms;
    public ObservableCollection<AlarmRecord> AlarmHistory => Shell.AlarmHistory;
    public ObservableCollection<AlarmRecord> Logs => Shell.Logs;
    public ObservableCollection<AlarmRecord> AlarmStatistics => Shell.AlarmStatistics;
    public ObservableCollection<string> AlarmLevelOptions => Shell.AlarmLevelOptions;
    public ObservableCollection<string> AlarmTimeRangeOptions => Shell.AlarmTimeRangeOptions;
    public ICollectionView AlarmStatisticsView => Shell.AlarmStatisticsView;
    public int ActiveAlarmCount => Shell.ActiveAlarmCount;
    public int UnacknowledgedAlarmCount => Shell.UnacknowledgedAlarmCount;
    public double EstimatedDowntimeMinutes => Shell.EstimatedDowntimeMinutes;
    public int EstimatedProductionLoss => Shell.EstimatedProductionLoss;
    public bool IsAlarmCurrentPageVisible => Shell.IsAlarmCurrentPageVisible;
    public bool IsAlarmHistoryPageVisible => Shell.IsAlarmHistoryPageVisible;
    public bool IsAlarmLogPageVisible => Shell.IsAlarmLogPageVisible;
    public bool IsAlarmStatisticsPageVisible => Shell.IsAlarmStatisticsPageVisible;
    public string FocusAlarmHint => Shell.FocusAlarmHint;
    public IRelayCommand<string?> JumpToFlowByAlarmCommand => Shell.JumpToFlowByAlarmCommand;

    public string SelectedAlarmLevel
    {
        get => Shell.SelectedAlarmLevel;
        set => Shell.SelectedAlarmLevel = value;
    }

    public string SelectedAlarmTimeRange
    {
        get => Shell.SelectedAlarmTimeRange;
        set => Shell.SelectedAlarmTimeRange = value;
    }

    public bool ShowOnlyFocusAlarms
    {
        get => Shell.ShowOnlyFocusAlarms;
        set => Shell.ShowOnlyFocusAlarms = value;
    }

    // A2/A9: 当前 + 历史共用的过滤
    public ICollectionView CurrentAlarmsView => Shell.CurrentAlarmsView;
    public ICollectionView AlarmHistoryView => Shell.AlarmHistoryView;
    public ObservableCollection<string> AlarmListLevelOptions => Shell.AlarmListLevelOptions;
    public ObservableCollection<string> AlarmListTimeRangeOptions => Shell.AlarmListTimeRangeOptions;
    public ObservableCollection<string> AlarmSourceOptions => Shell.AlarmSourceOptions;
    public string AlarmListLevelFilter
    {
        get => Shell.AlarmListLevelFilter;
        set => Shell.AlarmListLevelFilter = value;
    }
    public string AlarmListSourceFilter
    {
        get => Shell.AlarmListSourceFilter;
        set => Shell.AlarmListSourceFilter = value;
    }
    public string AlarmListKeyword
    {
        get => Shell.AlarmListKeyword;
        set => Shell.AlarmListKeyword = value;
    }
    public string AlarmListTimeRange
    {
        get => Shell.AlarmListTimeRange;
        set => Shell.AlarmListTimeRange = value;
    }

    // A3 详情侧边栏当前选中
    public AlarmRecord? SelectedAlarmDetail
    {
        get => Shell.SelectedAlarmDetail;
        set => Shell.SelectedAlarmDetail = value;
    }

    // A1 顶部高级别红条
    public string HighAlarmBannerText => Shell.HighAlarmBannerText;
    public IRelayCommand DismissHighAlarmBannerCommand => Shell.DismissHighAlarmBannerCommand;

    // A4 导出筛选 / A8 跳流程
    public IAsyncRelayCommand ExportFilteredAlarmsCommand => Shell.ExportFilteredAlarmsCommand;

    // A5 频率直方图
    public ObservableCollection<AlarmHistogramBar> AlarmFrequencyBars => Shell.AlarmFrequencyBars;

    public void AcknowledgeAllAlarms()
    {
        if (!Shell.CanOperateDevices)
        {
            Shell.SystemMessage = "当前权限不足，无法确认报警";
            return;
        }

        foreach (var alarm in CurrentAlarms)
        {
            alarm.Acknowledged = true;
            alarm.AcknowledgedBy = Shell.LoginUser;
            alarm.State = alarm.Active ? "Acknowledged" : "Cleared";
            if (string.IsNullOrWhiteSpace(alarm.HandlingSuggestion))
            {
                alarm.HandlingSuggestion = BuildHandlingSuggestion(alarm.Source, alarm.Level);
            }

            if (string.IsNullOrWhiteSpace(alarm.CauseArchive))
            {
                alarm.CauseArchive = BuildCauseArchive(alarm.Source);
            }
        }

        Shell.RefreshAlarmStatistics();
        Shell.SystemMessage = "当前报警已全部确认";
        Shell.AddLog("报警", Shell.SystemMessage, "Info");
    }

    public async Task ResetAllAlarmsAsync()
    {
        if (!Shell.CanAdmin)
        {
            Shell.SystemMessage = "仅管理员可复位报警";
            return;
        }

        foreach (var alarm in CurrentAlarms.ToList())
        {
            alarm.Active = false;
            alarm.Acknowledged = true;
            alarm.ClearTime = DateTime.Now;
            alarm.State = "Reset";
            AlarmHistory.Insert(0, new AlarmRecord
            {
                Time = DateTime.Now,
                Level = alarm.Level,
                Source = alarm.Source,
                Message = $"已复位：{alarm.Message}",
                Active = false,
                Acknowledged = true,
                ClearTime = DateTime.Now,
                State = "Reset",
                Count = alarm.Count
            });
        }

        CurrentAlarms.Clear();
        Shell.ClearActiveAlarmMap();
        Shell.NotifyAlarmStateChanged();
        Shell.RefreshAlarmStatistics();
        await SaveAlarmHistoryAsync();
        Shell.SystemMessage = "报警已全部复位";
        Shell.AddLog("报警", Shell.SystemMessage, "Warning");
    }

    public async Task SaveAlarmHistoryAsync()
    {
        var path = Path.Combine(Shell.GetProjectRoot(), "config", "alarm-history.json");
        await _alarmService.SaveHistoryAsync(path, AlarmHistory);
        Shell.SystemMessage = $"报警历史已保存：{path}";
    }

    public async Task LoadAlarmHistoryAsync()
    {
        var path = Path.Combine(Shell.GetProjectRoot(), "config", "alarm-history.json");
        var items = await _alarmService.LoadHistoryAsync(path);
        if (items.Count == 0)
        {
            return;
        }

        AlarmHistory.Clear();
        foreach (var item in items)
        {
            AlarmHistory.Add(item);
        }

        Shell.RefreshAlarmStatistics();
    }

    private static string BuildHandlingSuggestion(string source, string level) => source switch
    {
        var s when s.Contains("EStop", StringComparison.OrdinalIgnoreCase) => "优先检查安全回路、急停按钮与安全继电器",
        var s when s.Contains("Motor", StringComparison.OrdinalIgnoreCase) => "检查电机过载、接触器、驱动器与机械卡滞",
        var s when s.Contains("Axis", StringComparison.OrdinalIgnoreCase) => "检查伺服报警代码、编码器、负载与运动参数",
        var s when s.Contains("Air", StringComparison.OrdinalIgnoreCase) => "检查气源压力、过滤器、气管泄漏与阀组",
        var s when s.Contains("Vacuum", StringComparison.OrdinalIgnoreCase) => "检查真空发生器、吸盘漏气、真空开关与工件状态",
        _ => level switch
        {
            "Alarm" => "优先安排停机排查并记录根因",
            "Error" => "安排设备点检并确认恢复条件",
            "Warning" => "纳入巡检重点，观察是否重复发生",
            _ => "保留记录，持续观察"
        }
    };

    private static string BuildCauseArchive(string source) => source switch
    {
        var s when s.Contains("EStop", StringComparison.OrdinalIgnoreCase) => "安全保护动作/人工触发/回路异常",
        var s when s.Contains("Motor", StringComparison.OrdinalIgnoreCase) => "过载/堵转/接线松动/机构干涉",
        var s when s.Contains("Axis", StringComparison.OrdinalIgnoreCase) => "伺服参数异常/机械阻力/原点偏移",
        var s when s.Contains("Air", StringComparison.OrdinalIgnoreCase) => "供气不足/泄漏/过滤器堵塞",
        var s when s.Contains("Vacuum", StringComparison.OrdinalIgnoreCase) => "真空建立失败/吸附不良/工件偏移",
        _ => "待补充归档原因"
    };
}
