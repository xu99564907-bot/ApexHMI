#nullable enable
using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ApexHMI.Models;
using ApexHMI.Models.RuntimeUi;
using ApexHMI.Services.RuntimeUi;
using Microsoft.Win32;

namespace ApexHMI.ViewModels.Runtime;

/// <summary>
/// P5B 报警视图：对接 MainViewModel.CurrentAlarms / AlarmHistory（反射访问，避免循环引用）。
/// <para>属性：maxRows / filterLevel / filterSource / onlyActive / autoScroll / allowAck</para>
/// </summary>
public partial class AlarmViewWidgetViewModel : WidgetViewModelBase
{
    private IList? _sourceCollection;       // MainViewModel.CurrentAlarms 或 AlarmHistory（IList，AlarmRecord 元素）
    private INotifyCollectionChanged? _ncc;
    private object? _shellRef;

    [ObservableProperty]
    private bool _paused;

    public AlarmViewWidgetViewModel(WidgetInstance model, IWidgetDataContext dataContext)
        : base(model, dataContext)
    {
        AttachToShell();
        RebuildVisible();
    }

    public ObservableCollection<AlarmRecord> VisibleAlarms { get; } = new();

    public int MaxRows
    {
        get
        {
            var raw = Prop("maxRows", "100");
            return int.TryParse(raw, out var v) && v > 0 ? v : 100;
        }
    }

    public string FilterLevel  => Prop("filterLevel", "All");      // All / Info / Warning / Error / Alarm
    public string FilterSource => Prop("filterSource", "");
    public bool OnlyActive => string.Equals(Prop("onlyActive", "false"), "true", StringComparison.OrdinalIgnoreCase);
    public bool AutoScroll => string.Equals(Prop("autoScroll", "true"), "true", StringComparison.OrdinalIgnoreCase);
    public bool AllowAck   => string.Equals(Prop("allowAck", "true"), "true", StringComparison.OrdinalIgnoreCase);

    // ============ B3.1: 8 状态色暴露（DataGrid 行模板按 LifecycleState 选） ============
    public string ColorIn             => Prop("colorIn", "#DC2626");
    public string ColorInAck          => Prop("colorInAck", "#F59E0B");
    public string ColorCame           => Prop("colorCame", "#22C55E");
    public string ColorLeftInactive   => Prop("colorLeftInactive", "#94A3B8");
    public string ColorLeftConfirmed  => Prop("colorLeftConfirmed", "#64748B");
    public string ColorFlashing       => Prop("colorFlashing", "#FBBF24");
    public string ColorCleared        => Prop("colorCleared", "#CBD5E1");
    public string ColorHover          => Prop("colorHover", "#E0F2FE");
    public string ColorSelected       => Prop("colorSelected", "#BAE6FD");
    public bool LogOperation => string.Equals(Prop("logOperation", "false"), "true", StringComparison.OrdinalIgnoreCase);

    [ObservableProperty]
    private AlarmRecord? _selectedAlarm;

    private void AttachToShell()
    {
        var shell = _dataContext.Shell;
        _shellRef = shell;
        if (shell is null) return;

        var t = shell.GetType();
        // 优先 CurrentAlarms，没有 onlyActive 时还要 AlarmHistory；目前先订 CurrentAlarms
        var coll = (t.GetProperty("CurrentAlarms")?.GetValue(shell)
                    ?? t.GetProperty("AlarmHistory")?.GetValue(shell)) as IList;
        if (coll is null) return;
        _sourceCollection = coll;
        if (coll is INotifyCollectionChanged ncc)
        {
            _ncc = ncc;
            ncc.CollectionChanged += SourceChanged;
        }
    }

    private void SourceChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (Paused) return;
        // 在 UI 线程刷新
        if (Application.Current?.Dispatcher is { } d && !d.CheckAccess())
        {
            d.BeginInvoke(new Action(RebuildVisible));
        }
        else
        {
            RebuildVisible();
        }
    }

    private void RebuildVisible()
    {
        VisibleAlarms.Clear();
        if (_sourceCollection is null)
        {
            // 设计时无 Shell：放一条示例
            VisibleAlarms.Add(new AlarmRecord { Time = DateTime.Now, Level = "Warning", Source = "Demo", Message = "[设计时占位] 运行时显示真实报警", State = "Active", Active = true, Count = 1 });
            return;
        }

        var level = FilterLevel;
        var src   = FilterSource;
        var onlyA = OnlyActive;
        var max   = MaxRows;

        int added = 0;
        foreach (var obj in _sourceCollection)
        {
            if (obj is not AlarmRecord a) continue;
            if (!string.Equals(level, "All", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(a.Level, level, StringComparison.OrdinalIgnoreCase)) continue;
            if (!string.IsNullOrWhiteSpace(src)
                && a.Source.IndexOf(src, StringComparison.OrdinalIgnoreCase) < 0) continue;
            if (onlyA && !a.Active) continue;
            VisibleAlarms.Add(a);
            if (++added >= max) break;
        }
    }

    [RelayCommand]
    private void Refresh() => RebuildVisible();

    [RelayCommand]
    private void TogglePause()
    {
        Paused = !Paused;
        if (!Paused) RebuildVisible();
    }

    [RelayCommand]
    private void AckSelected()
    {
        if (!AllowAck || SelectedAlarm is null) return;
        if (!CheckAuthorizationAndNotify()) return;
        AckOne(SelectedAlarm);
    }

    [RelayCommand]
    private void AckAll()
    {
        if (!AllowAck) return;
        if (!CheckAuthorizationAndNotify()) return;
        // 复用 Shell 的命令（如果存在），否则手工标记
        var shell = _shellRef;
        if (shell is not null)
        {
            var t = shell.GetType();
            var cmdProp = t.GetProperty("AcknowledgeAllAlarmsCommand");
            if (cmdProp?.GetValue(shell) is System.Windows.Input.ICommand cmd && cmd.CanExecute(null))
            {
                cmd.Execute(null);
                return;
            }
        }
        foreach (var a in VisibleAlarms)
            AckOne(a);
    }

    /// <summary>B3.1: 单条 Ack — 写新 AckedAt/AckedBy + audit trail（按 logOperation 字段）。</summary>
    private void AckOne(AlarmRecord a)
    {
        if (a.Acknowledged) return;
        var who = GetCurrentUser();
        a.Acknowledged = true;
        a.AckedAt = DateTime.Now;
        a.AckedBy = who;
        a.AcknowledgedBy = who;
        a.State = a.Active ? "Acknowledged" : "Cleared";

        if (LogOperation)
        {
            TryWriteAudit("报警确认",
                target: $"{a.Source}/{a.Message}",
                result: "OK",
                detail: $"alarmTime={a.Time:yyyy-MM-dd HH:mm:ss} level={a.Level} state={a.LifecycleState}");
        }
    }

    private string GetCurrentUser()
    {
        var shell = _shellRef;
        if (shell is null) return "(designer)";
        var p = shell.GetType().GetProperty("LoginUser");
        return (p?.GetValue(shell) as string) ?? "(unknown)";
    }

    private void TryWriteAudit(string action, string target, string result, string detail)
    {
        var shell = _shellRef;
        if (shell is null)
        {
            Serilog.Log.Information("AlarmAck audit (no shell): {Action} target={Target} detail={Detail}",
                action, target, detail);
            return;
        }
        var m = shell.GetType().GetMethod("AddAudit");
        if (m is not null)
        {
            try { m.Invoke(shell, new object[] { action, target, result, detail }); return; }
            catch (Exception ex) { Serilog.Log.Warning(ex, "AlarmAck audit invoke 失败"); }
        }
        Serilog.Log.Information("AlarmAck audit: {Action} target={Target} detail={Detail}",
            action, target, detail);
    }

    [RelayCommand]
    private void ExportCsv()
    {
        var dlg = new SaveFileDialog
        {
            Filter = "CSV 文件|*.csv",
            FileName = $"alarms-{DateTime.Now:yyyyMMdd-HHmmss}.csv"
        };
        if (dlg.ShowDialog() != true) return;
        var sb = new StringBuilder();
        sb.AppendLine("Time,Level,Source,Message,State,Count,AcknowledgedBy");
        foreach (var a in VisibleAlarms)
        {
            sb.AppendLine(string.Join(",",
                a.Time.ToString("yyyy-MM-dd HH:mm:ss"),
                Esc(a.Level), Esc(a.Source), Esc(a.Message), Esc(a.State),
                a.Count, Esc(a.AcknowledgedBy)));
        }
        File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
    }

    private static string Esc(string? s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        if (s!.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0) return s;
        return "\"" + s.Replace("\"", "\"\"") + "\"";
    }
}
