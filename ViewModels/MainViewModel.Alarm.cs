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
using ApexHMI.Models;
using ApexHMI.Services;

namespace ApexHMI.ViewModels;

public partial class MainViewModel
{
    private int _suppressAlarmStatisticsRefreshDepth;
    private readonly AlarmNotificationService _alarmNotificationService = new();

    // ========== A2/A9: 报警过滤（当前 + 历史共用一组过滤条件） ==========
    [ObservableProperty]
    private string alarmListLevelFilter = "全部";  // 全部 / Alarm / Error / Warning / Info

    [ObservableProperty]
    private string alarmListSourceFilter = "全部"; // 全部 / 各 source 名

    [ObservableProperty]
    private string alarmListKeyword = string.Empty; // A9 关键字搜索

    [ObservableProperty]
    private string alarmListTimeRange = "全部";    // A9 时间段：全部 / 近1h / 近8h / 今日 / 近7天

    [ObservableProperty]
    private AlarmRecord? selectedAlarmDetail;       // A3 详情侧边栏当前选中

    // A2 来源下拉来源（动态根据 CurrentAlarms+AlarmHistory 推导出来）
    public ObservableCollection<string> AlarmSourceOptions { get; } = new() { "全部" };

    public ObservableCollection<string> AlarmListLevelOptions { get; } =
        new() { "全部", "Alarm", "Error", "Warning", "Info" };

    public ObservableCollection<string> AlarmListTimeRangeOptions { get; } =
        new() { "全部", "近1h", "近8h", "今日", "近7天" };

    // A5 频率直方图：24 个 bar（最近 24h，每小时一个）按级别分桶
    public ObservableCollection<AlarmHistogramBar> AlarmFrequencyBars { get; } = new();

    public ICollectionView CurrentAlarmsView => CollectionViewSource.GetDefaultView(CurrentAlarms);
    public ICollectionView AlarmHistoryView => CollectionViewSource.GetDefaultView(AlarmHistory);

    // A1 高级别报警未确认时的浮动横幅（顶部红条），点击 × 后清空
    [ObservableProperty]
    private string highAlarmBannerText = string.Empty;

    [RelayCommand]
    private void DismissHighAlarmBanner() => HighAlarmBannerText = string.Empty;

    partial void OnAlarmListLevelFilterChanged(string value) => RefreshAlarmListView();
    partial void OnAlarmListSourceFilterChanged(string value) => RefreshAlarmListView();
    partial void OnAlarmListKeywordChanged(string value) => RefreshAlarmListView();
    partial void OnAlarmListTimeRangeChanged(string value) => RefreshAlarmListView();

    // ========== 报警/审计/流程分析 ==========

    [RelayCommand]
    private void AcknowledgeAllAlarms()
    {
        if (!CanOperateDevices) { SystemMessage = "当前权限不足，无法确认报警"; return; }
        // A6: 二次确认（避免误点清掉整页未读）
        if (CurrentAlarms.Count > 0 && !RequestConfirmation("确认全部报警", $"确定要确认当前全部 {CurrentAlarms.Count} 条报警吗？此操作将把所有报警标记为已确认。"))
        {
            return;
        }
        foreach (var alarm in CurrentAlarms)
        {
            alarm.Acknowledged = true;
            alarm.AcknowledgedBy = LoginUser;
            alarm.State = alarm.Active ? "Acknowledged" : "Cleared";
            if (string.IsNullOrWhiteSpace(alarm.HandlingSuggestion)) alarm.HandlingSuggestion = BuildHandlingSuggestion(alarm.Source, alarm.Level);
            if (string.IsNullOrWhiteSpace(alarm.CauseArchive)) alarm.CauseArchive = BuildCauseArchive(alarm.Source);
        }
        RefreshAlarmStatistics();
        SystemMessage = "当前报警已全部确认";
        AddLog("报警", SystemMessage, "Info");
    }

    [RelayCommand]
    private async Task ResetAllAlarmsAsync()
    {
        if (!CanAdmin) { SystemMessage = "仅管理员可复位报警"; return; }

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
        _activeAlarmMap.Clear();
        OnPropertyChanged(nameof(AlarmCount));
        RefreshAlarmStatistics();
        await SaveAlarmHistoryAsync();
        SystemMessage = "报警已全部复位";
        AddLog("报警", SystemMessage, "Warning");
    }

    [RelayCommand]
    private async Task SaveAlarmHistoryAsync()
    {
        var path = Path.Combine(GetProjectRoot(), "config", "alarm-history.json");
        await _alarmService.SaveHistoryAsync(path, AlarmHistory);
        SystemMessage = $"报警历史已保存：{path}";
    }

    [RelayCommand]
    private async Task LoadAlarmHistoryAsync()
    {
        var path = Path.Combine(GetProjectRoot(), "config", "alarm-history.json");
        var items = await _alarmService.LoadHistoryAsync(path);
        if (items.Count == 0) return;
        AlarmHistory.Clear();
        foreach (var item in items) AlarmHistory.Add(item);
        RefreshAlarmStatistics();
    }

    private void EvaluateEvents(TagItem tag)
    {
        var bindings = EventBindings.Where(e => e.TagName.Equals(tag.Name, StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var binding in bindings)
        {
            var triggered = binding.TriggerCondition.Equals("ValueChanged", StringComparison.OrdinalIgnoreCase)
                            || binding.TriggerCondition.Equals(tag.CurrentValue, StringComparison.OrdinalIgnoreCase)
                            || (binding.TriggerCondition.Equals("True", StringComparison.OrdinalIgnoreCase) && tag.CurrentValue.Equals("True", StringComparison.OrdinalIgnoreCase));
            if (!triggered) continue;
            if (tag.IsAlarm) RaiseOrUpdateAlarm(tag.Name, binding.EventName, binding.ActionParameter);
            else Logs.Insert(0, new AlarmRecord { Time = DateTime.Now, Level = "Info", Source = tag.Name, Message = $"事件 {binding.EventName} -> {binding.ActionTarget} {binding.ActionParameter}", Active = false, Acknowledged = true, State = "Logged", Count = 1 });
        }
    }

    private void EvaluateTagState(TagItem tag)
    {
        if (!tag.IsAlarm) return;
        if (string.Equals(tag.CurrentValue, "True", StringComparison.OrdinalIgnoreCase)) RaiseOrUpdateAlarm(tag.Name, tag.Name, "报警触发");
        else ClearAlarm(tag.Name);
    }

    private void RaiseOrUpdateAlarm(string source, string eventName, string detail)
    {
        var key = $"{source}|{detail}";
        if (_activeAlarmMap.TryGetValue(key, out var existing))
        {
            existing.Count += 1;
            existing.Time = DateTime.Now;
            existing.State = existing.Acknowledged ? "Acknowledged" : "Active";
            RequestAlarmStatisticsRefresh();
            return;
        }

        var level = source.Contains("EStop", StringComparison.OrdinalIgnoreCase) ? "Alarm"
            : source.Contains("Fault", StringComparison.OrdinalIgnoreCase) ? "Alarm"
            : source.Contains("Axis", StringComparison.OrdinalIgnoreCase) ? "Alarm"
            : "Warning";

        var alarm = new AlarmRecord
        {
            Time = DateTime.Now,
            Level = level,
            Source = source,
            Message = string.IsNullOrWhiteSpace(detail) ? eventName : $"{eventName} - {detail}",
            Active = true,
            Acknowledged = false,
            State = "Active",
            Count = 1,
            HandlingSuggestion = BuildHandlingSuggestion(source, level),
            CauseArchive = BuildCauseArchive(source)
        };
        CurrentAlarms.Insert(0, alarm);
        AlarmHistory.Insert(0, new AlarmRecord { Time = alarm.Time, Level = alarm.Level, Source = alarm.Source, Message = alarm.Message, Active = true, Acknowledged = false, State = "Raised", Count = alarm.Count, HandlingSuggestion = alarm.HandlingSuggestion, CauseArchive = alarm.CauseArchive });
        _activeAlarmMap[key] = alarm;
        Logs.Insert(0, new AlarmRecord { Time = DateTime.Now, Level = "Warning", Source = source, Message = $"报警触发：{alarm.Message}", Active = false, Acknowledged = true, State = "Logged", Count = 1 });
        OnPropertyChanged(nameof(AlarmCount));
        RequestAlarmStatisticsRefresh();

        // A1: 高级别 + 未确认 → 播声音 + 顶部红条横幅
        if (level is "Alarm" or "Error")
        {
            _alarmNotificationService.PlaySoundForLevel(level);
            HighAlarmBannerText = $"[{level}] {alarm.Source} - {alarm.Message}";
            // A10: 异步推送到 alarm-notifications.log（外部 SMTP/IM 网关可监听该文件）
            _ = _alarmNotificationService.PushAsync(GetProjectRoot(), alarm);
        }
        else if (level == "Warning")
        {
            _alarmNotificationService.PlaySoundForLevel(level);
        }
    }

    private void ClearAlarm(string source)
    {
        var keys = _activeAlarmMap.Keys.Where(k => k.StartsWith(source + "|", StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var key in keys)
        {
            if (!_activeAlarmMap.TryGetValue(key, out var alarm)) continue;
            alarm.Active = false;
            alarm.ClearTime = DateTime.Now;
            alarm.State = alarm.Acknowledged ? "Cleared" : "Cleared-UnAck";
            CurrentAlarms.Remove(alarm);
            AlarmHistory.Insert(0, new AlarmRecord { Time = alarm.Time, Level = alarm.Level, Source = alarm.Source, Message = $"恢复：{alarm.Message}", Active = false, Acknowledged = alarm.Acknowledged, AcknowledgedBy = alarm.AcknowledgedBy, ClearTime = DateTime.Now, State = "Cleared", Count = alarm.Count, HandlingSuggestion = alarm.HandlingSuggestion, CauseArchive = alarm.CauseArchive });
            Logs.Insert(0, new AlarmRecord { Time = DateTime.Now, Level = "Info", Source = source, Message = $"报警恢复：{alarm.Message}", Active = false, Acknowledged = true, State = "Logged", Count = 1 });
            _activeAlarmMap.Remove(key);
        }
        OnPropertyChanged(nameof(AlarmCount));
        RequestAlarmStatisticsRefresh();
    }

    private void BeginBatchAlarmEvaluation()
    {
        _suppressAlarmStatisticsRefreshDepth++;
    }

    private void EndBatchAlarmEvaluation()
    {
        if (_suppressAlarmStatisticsRefreshDepth > 0)
        {
            _suppressAlarmStatisticsRefreshDepth--;
        }

        if (_suppressAlarmStatisticsRefreshDepth == 0)
        {
            RefreshAlarmStatistics();
        }
    }

    private void RequestAlarmStatisticsRefresh()
    {
        if (_suppressAlarmStatisticsRefreshDepth > 0)
        {
            return;
        }

        RefreshAlarmStatistics();
    }

    public void ClearActiveAlarmMap()
    {
        _activeAlarmMap.Clear();
    }

    public void NotifyAlarmStateChanged()
    {
        OnPropertyChanged(nameof(AlarmCount));
        UpdateRuntimeVisuals();
    }

    public void RefreshAlarmStatistics()
    {
        var merged = AlarmHistory
            .Concat(CurrentAlarms)
            .GroupBy(a => new { a.Source, a.Level })
            .Select(g => new AlarmRecord
            {
                Time = g.Max(x => x.Time),
                Level = g.Key.Level,
                Source = g.Key.Source,
                Message = $"{g.Key.Source} 累计 {g.Sum(x => Math.Max(1, x.Count))} 次",
                Active = g.Any(x => x.Active),
                Acknowledged = g.All(x => x.Acknowledged),
                State = g.Any(x => x.Active) ? "重点关注" : "已恢复",
                Count = g.Sum(x => Math.Max(1, x.Count))
            })
            .OrderByDescending(x => x.Count)
            .ThenByDescending(x => x.Active)
            .ToList();

        AlarmStatistics.Clear();
        foreach (var item in merged) AlarmStatistics.Add(item);

        var now = DateTime.Now;
        AlarmStatisticsView.Filter = item =>
        {
            if (item is not AlarmRecord alarm) return false;
            if (SelectedAlarmLevel != "全部" && !alarm.Level.Equals(SelectedAlarmLevel, StringComparison.OrdinalIgnoreCase)) return false;
            if (SelectedAlarmTimeRange == "本班次" && alarm.Time < now.AddHours(-8)) return false;
            if (SelectedAlarmTimeRange == "今日" && alarm.Time.Date != now.Date) return false;
            if (SelectedAlarmTimeRange == "近7天" && alarm.Time < now.AddDays(-7)) return false;
            if (ShowOnlyFocusAlarms && alarm.Count < 3 && !alarm.Active) return false;
            return true;
        };
        AlarmStatisticsView.Refresh();
        OnPropertyChanged(nameof(FocusAlarmHint));

        // A2/A5: 来源下拉 + 频率直方图同步刷新
        RefreshAlarmSourceOptions();
        RefreshAlarmFrequencyBars();
        RefreshAlarmListView();
    }

    [RelayCommand]
    private void JumpToAlarmPage()
    {
        Navigate("报警画面");
        SectionJumpRequested?.Invoke("报警画面", null);
    }

    [RelayCommand]
    private void JumpToAuditPage()
    {
        Navigate("操作审计");
        SectionJumpRequested?.Invoke("操作审计", null);
    }

    [RelayCommand]
    private void JumpToAlarmByKeyword(string? keyword)
    {
        JumpAlarmKeyword = keyword ?? string.Empty;
        HighlightAlarm(JumpAlarmKeyword);
        Navigate("当前报警");
        SectionJumpRequested?.Invoke("报警画面", JumpAlarmKeyword);
        HighlightRequested?.Invoke("Alarm", JumpAlarmKeyword);
    }

    [RelayCommand]
    private void JumpToFlowByAlarm(string? alarmKeyword)
    {
        if (string.IsNullOrWhiteSpace(alarmKeyword))
        {
            return;
        }

        var matched = FlowSteps.FirstOrDefault(x => x.RelatedAlarm.Contains(alarmKeyword, StringComparison.OrdinalIgnoreCase));
        if (matched is not null)
        {
            SelectedFlowFilter = matched.FlowName;
            SelectedFlowStepFilter = matched.StepNo.ToString();
            ShowOnlyAbnormalFlow = true;
            HighlightFlow(matched.FlowName, matched.StepNo);
            RefreshFlowView();
            HighlightRequested?.Invoke("Flow", $"{matched.FlowName}|{matched.StepNo}");
        }
        Navigate("程序监控");
        SectionJumpRequested?.Invoke("监视画面", alarmKeyword);
    }

    [RelayCommand]
    private async Task ExportFlowIssueReportAsync()
    {
        var dialog = new SaveFileDialog { Filter = "CSV 文件|*.csv", FileName = $"flow-issue-report-{DateTime.Now:yyyyMMdd-HHmmss}.csv" };
        if (dialog.ShowDialog() != true) return;
        var sb = new StringBuilder();
        sb.AppendLine("Category,Name,Metric,Conclusion");
        foreach (var item in FlowIssueSummaries)
        {
            sb.AppendLine($"{item.Category},{item.Name},{item.Metric},{item.Conclusion}");
        }
        sb.AppendLine();
        sb.AppendLine("FlowName,StepNo,StartTime,EndTime,DurationSeconds,Result,RelatedAlarm");
        foreach (var item in FlowSteps.Where(x => x.IsAbnormal))
        {
            sb.AppendLine($"{item.FlowName},{item.StepNo},{item.StartTime:yyyy-MM-dd HH:mm:ss},{item.EndTime:yyyy-MM-dd HH:mm:ss},{item.DurationSeconds:F2},{item.Result},{item.RelatedAlarm}");
        }
        await Compat.WriteAllTextAsync(dialog.FileName, sb.ToString(), Encoding.UTF8);
        SystemMessage = $"异常流程分析报告已导出：{dialog.FileName}";
        AddLog("流程分析", SystemMessage, "Info");
    }

    [RelayCommand]
    private async Task ImportFlowCsvAsync()
    {
        var dialog = new OpenFileDialog { Filter = "CSV 文件|*.csv|所有文件|*.*" };
        if (dialog.ShowDialog() != true) return;
        var items = await _flowLogCsvService.LoadAsync(dialog.FileName);
        if (items.Count == 0)
        {
            ShowPopup("导入失败", "未从 CSV 中读取到有效流程数据。", "Warning");
            return;
        }
        FlowSteps.Clear();
        foreach (var item in items.OrderByDescending(x => x.Time).Take(200)) FlowSteps.Add(item);
        var latest = FlowSteps.FirstOrDefault();
        if (latest is not null)
        {
            CurrentFlowStepNo = latest.StepNo;
            CurrentFlowComment = latest.Comment;
        }
        RefreshFlowView();
        RefreshFlowIssueSummaries();
        OnPropertyChanged(nameof(FlowStepTrendPath));
        OnPropertyChanged(nameof(SelectedFlowSummary));
        SystemMessage = $"已导入流程 CSV：{Path.GetFileName(dialog.FileName)}";
        AddLog("流程分析", SystemMessage, "Info");
    }

    [RelayCommand]
    private async Task ResetShiftCountersAsync()
    {
        if (!RequestConfirmation("确认操作", "确认执行班次计数清零吗？")) return;
        SetTagValue("Shift_ProductionCount", "0");
        SetTagValue("Shift_GoodCount", "0");
        SetTagValue("Shift_NgCount", "0");
        AddLog("生产", "班次计数已清零", "Info");
        UpdateRuntimeVisuals();
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task ResetDailyCountersAsync()
    {
        if (!RequestConfirmation("确认操作", "确认执行日累计计数清零吗？")) return;
        SetTagValue("Daily_ProductionCount", "0");
        SetTagValue("Daily_GoodCount", "0");
        SetTagValue("Daily_NgCount", "0");
        AddLog("生产", "日累计计数已清零", "Info");
        UpdateRuntimeVisuals();
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task ExportProductionReportAsync()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "CSV 鏂囦欢|*.csv",
            FileName = $"production-report-{DateTime.Now:yyyyMMdd-HHmmss}.csv"
        };
        if (dialog.ShowDialog() != true) return;

        var sb = new StringBuilder();
        sb.AppendLine("项目,值");
        sb.AppendLine($"工单号,{CurrentOrderText}");
        sb.AppendLine($"配方,{CurrentRecipeText}");
        sb.AppendLine($"总产量,{ProductionCount}");
        sb.AppendLine($"良品,{GoodCount}");
        sb.AppendLine($"不良,{NgCount}");
        sb.AppendLine($"班次产量,{ShiftProductionCount}");
        sb.AppendLine($"日累计,{DailyProductionCount}");
        sb.AppendLine($"目标,{TargetCount}");
        sb.AppendLine($"Availability,{AvailabilityRate.ToString("F1", CultureInfo.InvariantCulture)}");
        sb.AppendLine($"Performance,{PerformanceRate.ToString("F1", CultureInfo.InvariantCulture)}");
        sb.AppendLine($"Quality,{QualityRate.ToString("F1", CultureInfo.InvariantCulture)}");
        sb.AppendLine($"OEE,{OeeRate.ToString("F1", CultureInfo.InvariantCulture)}");
        sb.AppendLine($"预计停机分钟,{EstimatedDowntimeMinutes.ToString("F1", CultureInfo.InvariantCulture)}");
        sb.AppendLine($"预计影响产量,{EstimatedProductionLoss}");
        sb.AppendLine();
        sb.AppendLine("报警来源,级别,累计次数,状态,结论");
        foreach (var item in AlarmStatistics)
        {
            sb.AppendLine($"{item.Source},{item.Level},{item.Count},{item.State},{item.Message}");
        }

        await Compat.WriteAllTextAsync(dialog.FileName, sb.ToString(), Encoding.UTF8);

        var excelPath = Path.ChangeExtension(dialog.FileName, ".xls");
        var html = new StringBuilder();
        html.AppendLine("<html><meta charset='utf-8'><body>");
        html.AppendLine("<table border='1'><tr><th>项目</th><th>值</th></tr>");
        html.AppendLine($"<tr><td>工单号</td><td>{CurrentOrderText}</td></tr>");
        html.AppendLine($"<tr><td>配方</td><td>{CurrentRecipeText}</td></tr>");
        html.AppendLine($"<tr><td>总产量</td><td>{ProductionCount}</td></tr>");
        html.AppendLine($"<tr><td>良品</td><td>{GoodCount}</td></tr>");
        html.AppendLine($"<tr><td>不良</td><td>{NgCount}</td></tr>");
        html.AppendLine($"<tr><td>OEE</td><td>{OeeRate:F1}%</td></tr>");
        html.AppendLine($"<tr><td>预计停机分钟</td><td>{EstimatedDowntimeMinutes:F1}</td></tr>");
        html.AppendLine($"<tr><td>预计影响产量</td><td>{EstimatedProductionLoss}</td></tr>");
        html.AppendLine("</table><br/>");
        html.AppendLine("<table border='1'><tr><th>报警来源</th><th>级别</th><th>累计次数</th><th>状态</th><th>建议</th><th>原因归档</th></tr>");
        foreach (var item in AlarmStatistics)
        {
            html.AppendLine($"<tr><td>{item.Source}</td><td>{item.Level}</td><td>{item.Count}</td><td>{item.State}</td><td>{item.HandlingSuggestion}</td><td>{item.CauseArchive}</td></tr>");
        }
        html.AppendLine("</table></body></html>");
        await Compat.WriteAllTextAsync(excelPath, html.ToString(), Encoding.UTF8);

        SystemMessage = "报表已导出：CSV + Excel 兼容文件";
        AddLog("报表", SystemMessage, "Info");
    }

    private void HighlightAlarm(string keyword)
    {
        foreach (var item in CurrentAlarms) item.IsHighlighted = false;
        foreach (var item in AlarmHistory) item.IsHighlighted = false;
        if (string.IsNullOrWhiteSpace(keyword)) return;
        foreach (var item in CurrentAlarms.Where(x => x.Source.Contains(keyword, StringComparison.OrdinalIgnoreCase) || x.Message.Contains(keyword, StringComparison.OrdinalIgnoreCase))) item.IsHighlighted = true;
        foreach (var item in AlarmHistory.Where(x => x.Source.Contains(keyword, StringComparison.OrdinalIgnoreCase) || x.Message.Contains(keyword, StringComparison.OrdinalIgnoreCase))) item.IsHighlighted = true;
    }

    private void HighlightFlow(string flowName, int stepNo)
    {
        foreach (var item in FlowSteps) item.IsHighlighted = false;
        foreach (var item in FlowSteps.Where(x => x.FlowName.Equals(flowName, StringComparison.OrdinalIgnoreCase) && x.StepNo == stepNo)) item.IsHighlighted = true;
    }

    private void RefreshFlowView()
    {
        var now = DateTime.Now;
        FlowStepsView.Filter = item =>
        {
            if (item is not FlowStepRecord step) return false;
            if (SelectedFlowFilter != "全部" && !step.FlowName.Equals(SelectedFlowFilter, StringComparison.OrdinalIgnoreCase)) return false;
            if (SelectedFlowTimeRange == "本班次" && step.Time < now.AddHours(-8)) return false;
            if (SelectedFlowTimeRange == "今日" && step.Time.Date != now.Date) return false;
            if (SelectedFlowTimeRange == "近7天" && step.Time < now.AddDays(-7)) return false;
            if (SelectedFlowStepFilter != "全部" && step.StepNo.ToString() != SelectedFlowStepFilter) return false;
            if (ShowOnlyAbnormalFlow && !step.IsAbnormal) return false;
            return true;
        };
        FlowStepsView.Refresh();
    }

    private void RefreshFlowIssueSummaries()
    {
        FlowIssueSummaries.Clear();
        var source = FlowSteps.AsEnumerable();
        if (!source.Any())
        {
            OnPropertyChanged(nameof(FlowRankingSummary));
            OnPropertyChanged(nameof(FlowIssueTrendPath));
            return;
        }

        var topAbnormalFlow = source.Where(x => x.IsAbnormal)
            .GroupBy(x => x.FlowName)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault();
        if (topAbnormalFlow is not null)
        {
            FlowIssueSummaries.Add(new FlowIssueSummary
            {
                Category = "异常最多流程",
                Name = topAbnormalFlow.Key,
                Metric = $"{topAbnormalFlow.Count()} 次",
                Conclusion = $"{topAbnormalFlow.Key} 异常次数最高，需要优先排查"
            });
        }

        var topAbnormalStep = source.Where(x => x.IsAbnormal)
            .GroupBy(x => x.StepNo)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault();
        if (topAbnormalStep is not null)
        {
            FlowIssueSummaries.Add(new FlowIssueSummary
            {
                Category = "最易异常步骤",
                Name = $"STEP {topAbnormalStep.Key:000}",
                Metric = $"{topAbnormalStep.Count()} 次",
                Conclusion = "该步骤最容易出现异常，可重点分析联锁与等待条件"
            });
        }

        var longestStep = source.GroupBy(x => x.StepNo)
            .Select(g => new { StepNo = g.Key, Avg = g.Average(x => x.DurationSeconds) })
            .OrderByDescending(x => x.Avg)
            .FirstOrDefault();
        if (longestStep is not null)
        {
            FlowIssueSummaries.Add(new FlowIssueSummary
            {
                Category = "平均耗时最长",
                Name = $"STEP {longestStep.StepNo:000}",
                Metric = $"{longestStep.Avg:F2} s",
                Conclusion = "该步骤平均耗时最长，建议检查机构节拍、等待信号与工艺时序"
            });
        }

        OnPropertyChanged(nameof(FlowRankingSummary));
        OnPropertyChanged(nameof(FlowIssueTrendPath));
    }

    private string ResolveRelatedAlarm(string flowName, int stepNo)
    {
        var alarms = CurrentAlarms.Concat(AlarmHistory).ToList();
        if (!alarms.Any()) return string.Empty;

        if (flowName == "主线1" && stepNo >= 40)
        {
            return alarms.FirstOrDefault(a => a.Source.Contains("Vacuum", StringComparison.OrdinalIgnoreCase) || a.Message.Contains("真空", StringComparison.OrdinalIgnoreCase))?.Message ?? string.Empty;
        }
        if (flowName == "主线2" && stepNo >= 30)
        {
            return alarms.FirstOrDefault(a => a.Source.Contains("Axis", StringComparison.OrdinalIgnoreCase) || a.Message.Contains("轴", StringComparison.OrdinalIgnoreCase))?.Message ?? string.Empty;
        }
        if (flowName == "主线3" && stepNo >= 20)
        {
            return alarms.FirstOrDefault(a => a.Source.Contains("Air", StringComparison.OrdinalIgnoreCase) || a.Message.Contains("气压", StringComparison.OrdinalIgnoreCase) || a.Source.Contains("Motor", StringComparison.OrdinalIgnoreCase))?.Message ?? string.Empty;
        }

        return string.Empty;
    }

    private void SeedFlowSteps()
    {
        FlowSteps.Clear();
        var now = DateTime.Now;
        FlowSteps.Add(new FlowStepRecord { FlowId = "F1", FlowName = "主线1", Time = now.AddSeconds(-30), StartTime = now.AddSeconds(-33), EndTime = now.AddSeconds(-30), DurationSeconds = 3.0, StepNo = 10, Icon = "●", Title = "上料到位", Comment = "工件进入取料位", Result = "完成", ShiftKey = "白班", ArchiveDate = now.ToString("yyyy-MM-dd") });
        FlowSteps.Add(new FlowStepRecord { FlowId = "F1", FlowName = "主线1", Time = now.AddSeconds(-24), StartTime = now.AddSeconds(-27), EndTime = now.AddSeconds(-24), DurationSeconds = 3.0, StepNo = 20, Icon = "●", Title = "气缸夹紧", Comment = "夹紧缸动作完成", Result = "完成", ShiftKey = "白班", ArchiveDate = now.ToString("yyyy-MM-dd") });
        FlowSteps.Add(new FlowStepRecord { FlowId = "F2", FlowName = "主线2", Time = now.AddSeconds(-18), StartTime = now.AddSeconds(-22), EndTime = now.AddSeconds(-18), DurationSeconds = 4.0, StepNo = 30, Icon = "●", Title = "轴移动定位", Comment = "轴到达装配工位", Result = "完成", ShiftKey = "白班", ArchiveDate = now.ToString("yyyy-MM-dd") });
        FlowSteps.Add(new FlowStepRecord { FlowId = "F3", FlowName = "主线3", Time = now.AddSeconds(-12), StartTime = now.AddSeconds(-17), EndTime = now.AddSeconds(-12), DurationSeconds = 5.0, StepNo = 40, Icon = "●", Title = "机械手取放", Comment = "等待真空确认信号", Result = "运行中", ShiftKey = "白班", ArchiveDate = now.ToString("yyyy-MM-dd") });
        CurrentFlowStepNo = 40;
        CurrentFlowComment = "等待真空确认信号，自动流程正在执行 STEP040";
    }

    private void SimulateFlowProgress()
    {
        if (!GetBoolTag("Device_Start"))
        {
            CurrentFlowComment = "设备停止，自动流程待机";
            return;
        }

        var now = DateTime.Now;
        var flows = new[]
        {
            new { Id = "F1", Name = "主线1", BaseStep = 10 },
            new { Id = "F2", Name = "主线2", BaseStep = 20 },
            new { Id = "F3", Name = "主线3", BaseStep = 30 }
        };

        foreach (var flow in flows)
        {
            var latest = FlowSteps.FirstOrDefault(f => f.FlowId == flow.Id);
            var nextStep = latest is null ? flow.BaseStep : latest.StepNo + 10;
            if (nextStep > 60) nextStep = 10;
            var comment = nextStep switch
            {
                10 => $"{flow.Name} 上料检测完成，等待夹紧动作",
                20 => $"{flow.Name} 夹紧完成，准备轴定位",
                30 => $"{flow.Name} 轴定位完成，等待机械手动作",
                40 => $"{flow.Name} 机械手取放中，等待真空确认",
                50 => $"{flow.Name} 装配执行中，等待过站条件",
                60 => $"{flow.Name} 下料完成，准备进入下一循环",
                _ => $"{flow.Name} 自动流程运行中"
            };

            var startTime = latest?.EndTime ?? now.AddSeconds(-3);
            var relatedAlarm = ResolveRelatedAlarm(flow.Name, nextStep);
            var abnormal = !string.IsNullOrWhiteSpace(relatedAlarm);
            var duration = Math.Round((now - startTime).TotalSeconds, 2);
            var timeout = nextStep >= 40 ? 4.0 : 3.0;
            if (duration > timeout && !abnormal)
            {
                abnormal = true;
                relatedAlarm = $"STEP{nextStep:000} 卡步超时 {duration:F2}s";
                AddLog("流程超时", relatedAlarm, "Warning");
            }

            var step = new FlowStepRecord
            {
                FlowId = flow.Id,
                FlowName = flow.Name,
                Time = now,
                StartTime = startTime,
                EndTime = now,
                DurationSeconds = duration,
                StepNo = nextStep,
                Icon = abnormal ? "!" : nextStep == 40 ? ">" : "●",
                Title = $"{flow.Name} STEP{nextStep:000}",
                Comment = comment,
                Result = abnormal ? "异常监视" : "运行中",
                RelatedAlarm = relatedAlarm ?? string.Empty,
                IsAbnormal = abnormal,
                ShiftKey = "白班",
                ArchiveDate = now.ToString("yyyy-MM-dd")
            };

            FlowSteps.Insert(0, step);
            if (IsAutoMode)
            {
                _ = SaveFlowStepCsvAsync(step);
                _ = SaveFlowStepArchiveAsync(step);
            }
        }

        while (FlowSteps.Count > 120)
        {
            FlowSteps.RemoveAt(FlowSteps.Count - 1);
        }

        var head = FlowSteps.FirstOrDefault();
        if (head is not null)
        {
            CurrentFlowStepNo = head.StepNo;
            CurrentFlowComment = head.Comment;
        }

        OnPropertyChanged(nameof(CurrentFlowStepText));
        OnPropertyChanged(nameof(FlowStepTrendPath));
        OnPropertyChanged(nameof(SelectedFlowSummary));
        RefreshFlowView();
        RefreshFlowIssueSummaries();
    }

    private async Task SaveFlowStepCsvAsync(FlowStepRecord step)
    {
        var path = Path.Combine(GetProjectRoot(), "config", "flow-steps.csv");
        await _flowLogCsvService.AppendAsync(path, step);
    }

    private async Task SaveFlowStepArchiveAsync(FlowStepRecord step)
    {
        var fileName = $"flow-{step.FlowName}-{step.ArchiveDate}-{step.ShiftKey}.csv";
        var path = Path.Combine(GetProjectRoot(), "config", "flow-archive", fileName);
        await _flowLogCsvService.AppendAsync(path, step);
    }

    private async Task SaveTrendHistoryAsync()
    {
        var path = Path.Combine(GetProjectRoot(), "config", "trend-history.csv");
        var now = DateTime.Now;
        var samples = new List<TrendSample>
        {
            new() { Time = now, Category = "OEE", Value = OeeRate, Source = "OEE" },
            new() { Time = now, Category = "Production", Value = ProductionCount, Source = "Production_Count" },
            new() { Time = now, Category = "Alarm", Value = ActiveAlarmCount, Source = "ActiveAlarmCount" }
        };
        foreach (var sample in samples)
        {
            TrendSamples.Insert(0, sample);
        }
        while (TrendSamples.Count > 300)
        {
            TrendSamples.RemoveAt(TrendSamples.Count - 1);
        }
        await _trendHistoryService.AppendAsync(path, samples);
    }

    private void SeedTrendSamples()
    {
        TrendSamples.Clear();
        var now = DateTime.Now;
        TrendSamples.Add(new TrendSample { Time = now.AddMinutes(-25), Category = "OEE", Value = 76.2, Source = "System" });
        TrendSamples.Add(new TrendSample { Time = now.AddMinutes(-20), Category = "OEE", Value = 78.1, Source = "System" });
        TrendSamples.Add(new TrendSample { Time = now.AddMinutes(-15), Category = "Production", Value = 980, Source = "Production_Count" });
        TrendSamples.Add(new TrendSample { Time = now.AddMinutes(-10), Category = "Production", Value = 1110, Source = "Production_Count" });
        TrendSamples.Add(new TrendSample { Time = now.AddMinutes(-5), Category = "Alarm", Value = 3, Source = "ActiveAlarmCount" });
        TrendSamples.Add(new TrendSample { Time = now, Category = "Alarm", Value = ActiveAlarmCount, Source = "ActiveAlarmCount" });
    }

    [RelayCommand]
    private async Task LoadTrendHistoryAsync()
    {
        var path = Path.Combine(GetProjectRoot(), "config", "trend-history.csv");
        var items = await _trendHistoryService.LoadAsync(path);
        if (items.Count == 0)
        {
            AddLog("趋势", "未找到历史趋势文件，保留当前示例历史数据", "Info");
            return;
        }
        TrendSamples.Clear();
        foreach (var item in items.OrderByDescending(x => x.Time).Take(200)) TrendSamples.Add(item);
        AddLog("趋势", "历史趋势加载完成", "Info");
    }

    private string BuildFlowRankingSummary()
    {
        var source = FlowSteps.AsEnumerable();
        if (!source.Any()) return "暂无流程排行数据";

        var topAvgCycle = source.GroupBy(x => x.FlowName)
            .Select(g => new { Name = g.Key, Avg = g.Average(x => x.DurationSeconds) })
            .OrderByDescending(x => x.Avg)
            .FirstOrDefault();

        var topAlarmStep = source.Where(x => x.IsAbnormal)
            .GroupBy(x => x.StepNo)
            .Select(g => new { Step = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .FirstOrDefault();

        var longestSingle = source.OrderByDescending(x => x.DurationSeconds).FirstOrDefault();

        return $"平均节拍最长：{topAvgCycle?.Name ?? "--"} {topAvgCycle?.Avg:F2}s | 最多报警步骤：STEP {topAlarmStep?.Step ?? 0:000} {topAlarmStep?.Count ?? 0}次 | 最长卡步：{longestSingle?.FlowName ?? "--"} STEP {longestSingle?.StepNo ?? 0:000} {longestSingle?.DurationSeconds ?? 0:F2}s";
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

    // ========== A2/A3/A5/A9: 视图刷新与辅助 ==========

    /// <summary>A2/A9: 刷新当前报警 + 历史报警两个 ICollectionView 的 Filter（共用同一组过滤条件）。</summary>
    public void RefreshAlarmListView()
    {
        var now = DateTime.Now;
        bool predicate(object item)
        {
            if (item is not AlarmRecord a) return false;
            if (!string.Equals(AlarmListLevelFilter, "全部", StringComparison.Ordinal)
                && !string.Equals(a.Level, AlarmListLevelFilter, StringComparison.OrdinalIgnoreCase)) return false;
            if (!string.Equals(AlarmListSourceFilter, "全部", StringComparison.Ordinal)
                && !string.Equals(a.Source, AlarmListSourceFilter, StringComparison.OrdinalIgnoreCase)) return false;
            if (AlarmListTimeRange == "近1h" && a.Time < now.AddHours(-1)) return false;
            if (AlarmListTimeRange == "近8h" && a.Time < now.AddHours(-8)) return false;
            if (AlarmListTimeRange == "今日" && a.Time.Date != now.Date) return false;
            if (AlarmListTimeRange == "近7天" && a.Time < now.AddDays(-7)) return false;
            var keyword = AlarmListKeyword?.Trim();
            if (!string.IsNullOrEmpty(keyword)
                && a.Source.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) < 0
                && a.Message.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) < 0
                && (a.Note ?? string.Empty).IndexOf(keyword, StringComparison.OrdinalIgnoreCase) < 0) return false;
            return true;
        }

        CurrentAlarmsView.Filter = predicate;
        AlarmHistoryView.Filter = predicate;
        CurrentAlarmsView.Refresh();
        AlarmHistoryView.Refresh();
    }

    /// <summary>A2: 根据 CurrentAlarms+AlarmHistory 推导出可用 source 列表。</summary>
    public void RefreshAlarmSourceOptions()
    {
        var sources = CurrentAlarms.Concat(AlarmHistory)
            .Select(a => a.Source)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();

        AlarmSourceOptions.Clear();
        AlarmSourceOptions.Add("全部");
        foreach (var s in sources) AlarmSourceOptions.Add(s);

        // 选中的 source 不在新列表中 → 重置
        if (!AlarmSourceOptions.Contains(AlarmListSourceFilter, StringComparer.Ordinal))
        {
            AlarmListSourceFilter = "全部";
        }
    }

    /// <summary>A5: 刷新最近 24h 频率直方图（24 个 bin，按级别分桶）。</summary>
    public void RefreshAlarmFrequencyBars()
    {
        var now = DateTime.Now;
        var hourStart = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0);
        var bars = new List<AlarmHistogramBar>();

        // 23 小时前的整点 → 当前小时，共 24 个 bin
        for (var i = 23; i >= 0; i--)
        {
            var binStart = hourStart.AddHours(-i);
            var binEnd = binStart.AddHours(1);
            bars.Add(new AlarmHistogramBar { Label = binStart.ToString("HH") });

            foreach (var a in AlarmHistory.Concat(CurrentAlarms))
            {
                if (a.Time < binStart || a.Time >= binEnd) continue;
                var bar = bars[bars.Count - 1];
                if (string.Equals(a.Level, "Alarm", StringComparison.OrdinalIgnoreCase)) bar.AlarmCount++;
                else if (string.Equals(a.Level, "Error", StringComparison.OrdinalIgnoreCase)) bar.ErrorCount++;
                else if (string.Equals(a.Level, "Warning", StringComparison.OrdinalIgnoreCase)) bar.WarningCount++;
            }
        }

        var max = bars.Max(b => b.Total);
        if (max <= 0) max = 1;
        foreach (var b in bars)
        {
            b.NormalizedHeight = (double)b.Total / max;
        }

        AlarmFrequencyBars.Clear();
        foreach (var b in bars) AlarmFrequencyBars.Add(b);
    }

    /// <summary>A4: 导出当前筛选结果为 CSV（CurrentAlarms 和 AlarmHistory 合并的过滤后视图）。</summary>
    [RelayCommand]
    private async Task ExportFilteredAlarmsAsync()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "CSV 文件|*.csv",
            FileName = $"alarms-filtered-{DateTime.Now:yyyyMMdd-HHmmss}.csv"
        };
        if (dialog.ShowDialog() != true) return;

        var sb = new StringBuilder();
        sb.AppendLine("时间,级别,来源,内容,状态,次数,确认人,备注,处理建议,关联流程");

        IEnumerable<AlarmRecord> Combine()
        {
            foreach (var a in CurrentAlarmsView.Cast<AlarmRecord>()) yield return a;
            foreach (var a in AlarmHistoryView.Cast<AlarmRecord>()) yield return a;
        }

        foreach (var a in Combine())
        {
            sb.AppendLine(string.Join(",",
                a.Time.ToString("yyyy-MM-dd HH:mm:ss"),
                EscapeAlarmCsvField(a.Level),
                EscapeAlarmCsvField(a.Source),
                EscapeAlarmCsvField(a.Message),
                EscapeAlarmCsvField(a.State),
                a.Count.ToString(CultureInfo.InvariantCulture),
                EscapeAlarmCsvField(a.AcknowledgedBy),
                EscapeAlarmCsvField(a.Note),
                EscapeAlarmCsvField(a.HandlingSuggestion),
                EscapeAlarmCsvField(a.RelatedFlowStep)));
        }

        await Compat.WriteAllTextAsync(dialog.FileName, sb.ToString(), Encoding.UTF8);
        SystemMessage = $"已导出筛选结果：{dialog.FileName}";
        AddLog("报警", SystemMessage, "Info");
    }

    private static string EscapeAlarmCsvField(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0) return value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
