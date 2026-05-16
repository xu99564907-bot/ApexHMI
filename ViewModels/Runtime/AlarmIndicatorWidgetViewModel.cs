#nullable enable
using System;
using System.Collections;
using System.Collections.Specialized;
using System.Reflection;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ApexHMI.Models;
using ApexHMI.Models.RuntimeUi;
using ApexHMI.Services.RuntimeUi;

namespace ApexHMI.ViewModels.Runtime;

/// <summary>
/// P8D 报警指示器：订阅 Shell.CurrentAlarms（反射），显示活动报警数。
/// <para>点击触发 navigate 跳到 <c>targetPage</c> 配置的 RouteKey。</para>
/// <para>blinkOnNew=true 时新报警进入会触发 IsNewAlarm 闪一次（由 View 通过样式响应）。</para>
/// </summary>
public partial class AlarmIndicatorWidgetViewModel : WidgetViewModelBase
{
    private IList? _alarms;
    private INotifyCollectionChanged? _ncc;

    public AlarmIndicatorWidgetViewModel(WidgetInstance model, IWidgetDataContext dataContext)
        : base(model, dataContext)
    {
        Attach();
        Recount();
    }

    public string TargetPage => Prop("targetPage", "");
    public string FilterLevel => Prop("filterLevel", "All");
    public bool BlinkOnNew => string.Equals(Prop("blinkOnNew", "true"), "true", StringComparison.OrdinalIgnoreCase);
    public string IndicatorColor => Prop("indicatorColor", "#DC2626");
    public string Foreground => Prop("foreground", "#FFFFFF");

    [ObservableProperty] private int _activeCount;
    [ObservableProperty] private bool _hasAlarms;
    [ObservableProperty] private bool _isNewAlarm;

    private void Attach()
    {
        var shell = _dataContext.Shell;
        if (shell is null) return;
        var prop = shell.GetType().GetProperty("CurrentAlarms",
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
        if (prop?.GetValue(shell) is IList coll)
        {
            _alarms = coll;
            if (coll is INotifyCollectionChanged ncc)
            {
                _ncc = ncc;
                ncc.CollectionChanged += OnAlarmsChanged;
            }
        }
    }

    private void OnAlarmsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (Application.Current?.Dispatcher is { } d && !d.CheckAccess())
        {
            d.BeginInvoke(new Action(() =>
            {
                Recount();
                if (BlinkOnNew && e.Action == NotifyCollectionChangedAction.Add) FlashNew();
            }));
        }
        else
        {
            Recount();
            if (BlinkOnNew && e.Action == NotifyCollectionChangedAction.Add) FlashNew();
        }
    }

    private void FlashNew()
    {
        IsNewAlarm = true;
        // 简易延迟复位（700ms）
        var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
        t.Tick += (_, _) => { IsNewAlarm = false; t.Stop(); };
        t.Start();
    }

    private void Recount()
    {
        if (_alarms is null)
        {
            ActiveCount = 0;
            HasAlarms = false;
            return;
        }
        int n = 0;
        var lvl = FilterLevel;
        foreach (var obj in _alarms)
        {
            if (obj is not AlarmRecord a) continue;
            if (!a.Active) continue;
            if (!string.Equals(lvl, "All", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(a.Level, lvl, StringComparison.OrdinalIgnoreCase)) continue;
            n++;
        }
        ActiveCount = n;
        HasAlarms = n > 0;
    }

    [RelayCommand]
    private void Click()
    {
        if (string.IsNullOrWhiteSpace(TargetPage)) return;
        _dataContext.ExecuteAction("navigate", TargetPage);
    }
}
