using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Threading;
using ApexHMI.Models.Production;
using ApexHMI.Services.Production;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;

namespace ApexHMI.ViewModels.Modules;

/// <summary>
/// 生产计数页 ViewModel。从 IProductionCountService 取分时 / 班次 / 31 天历史，
/// 通过 EventInserted 事件 + DispatcherTimer 1s 节流刷新避免 UI 卡顿。
/// </summary>
public sealed partial class CountViewModel : ModuleViewModelBase
{
    private readonly IProductionCountService _countSvc;
    private readonly DispatcherTimer _refreshTimer;
    private bool _pendingRefresh;

    public CountViewModel(MainViewModel shell, IProductionCountService countSvc)
        : base(shell, "生产计数")
    {
        _countSvc = countSvc;

        // 1s 节流：高频 EventInserted 累计后只刷新一次
        _refreshTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1) };
        _refreshTimer.Tick += (_, _) =>
        {
            if (!_pendingRefresh) return;
            _pendingRefresh = false;
            try { Refresh(); }
            catch (Exception ex) { Log.Warning(ex, "CountViewModel: 刷新失败"); }
        };
        _refreshTimer.Start();

        _countSvc.EventInserted += OnEventInserted;

        Refresh();
    }

    /// <summary>"Total" = OK + NG（HMI 合成）；"OK"；"NG"。</summary>
    [ObservableProperty]
    private string _selectedSource = "Total";

    /// <summary>白班合计（按 ShiftOptions.DayStart ~ NightStart）。</summary>
    [ObservableProperty]
    private int _dayTotal;

    /// <summary>夜班合计（按 ShiftOptions.NightStart ~ 次日 DayStart）。</summary>
    [ObservableProperty]
    private int _nightTotal;

    /// <summary>今日合计 = 白班 + 夜班。</summary>
    [ObservableProperty]
    private int _todayTotal;

    /// <summary>顶部切换按钮可选项。</summary>
    public IReadOnlyList<string> SourceOptions { get; } = new[] { "Total", "OK", "NG" };

    /// <summary>今日 24 个分时桶（按白班开始时间归桶）。</summary>
    public ObservableCollection<HourBucket> Hours { get; } = new();

    /// <summary>最近 31 天每天总和（含今天）。</summary>
    public ObservableCollection<DailyTotal> History { get; } = new();

    [RelayCommand]
    private void SwitchSource(string? source)
    {
        if (string.IsNullOrEmpty(source)) return;
        SelectedSource = source;
    }

    [RelayCommand]
    private void RefreshNow() => Refresh();

    partial void OnSelectedSourceChanged(string value) => Refresh();

    private void OnEventInserted(string source) => _pendingRefresh = true;

    /// <summary>同步刷新所有面板。UI 线程调用（DispatcherTimer.Tick 已在 UI 线程）。</summary>
    private void Refresh()
    {
        var src = SelectedSource;

        var hourly = _countSvc.GetHourlyToday(src);
        Hours.Clear();
        foreach (var h in hourly) Hours.Add(h);

        var shifts = _countSvc.GetShiftTotals(src);
        DayTotal = shifts.Day;
        NightTotal = shifts.Night;
        TodayTotal = shifts.Day + shifts.Night;

        var history = _countSvc.GetDailyHistory(src, 31);
        History.Clear();
        foreach (var d in history) History.Add(d);
    }
}
