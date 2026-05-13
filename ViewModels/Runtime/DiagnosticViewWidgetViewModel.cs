#nullable enable
using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using ApexHMI.Interfaces;
using ApexHMI.Models.RuntimeUi;
using ApexHMI.Services.RuntimeUi;

namespace ApexHMI.ViewModels.Runtime;

/// <summary>
/// P8C 系统诊断视图：通讯状态 / PLC 状态 / HMI 资源 三段卡片。
/// <para>通讯状态：反射取 Shell 的 IOpcUaService（IsConnected / ConnectionStatus / SubscribedTagNames）。</para>
/// <para>PLC 状态：第一版仅展示订阅 Tag 数；后续可加 PLC RunStop 节点轮询。</para>
/// <para>HMI 资源：进程 CPU / 内存（基于 Process.GetCurrentProcess + PerformanceCounter Total CPU 折算）。</para>
/// </summary>
public partial class DiagnosticViewWidgetViewModel : WidgetViewModelBase, IDisposable
{
    private readonly DispatcherTimer _timer;
    private IOpcUaService? _opc;
    private PerformanceCounter? _cpuCounter;

    private TimeSpan _lastProcessCpu;
    private DateTime _lastSampleAt;

    public DiagnosticViewWidgetViewModel(WidgetInstance model, IWidgetDataContext dataContext)
        : base(model, dataContext)
    {
        ResolveOpcService();
        TryInitPerf();

        _lastProcessCpu = Process.GetCurrentProcess().TotalProcessorTime;
        _lastSampleAt = DateTime.Now;

        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(Math.Max(1, RefreshIntervalSec))
        };
        _timer.Tick += (_, _) => Refresh();
        // 设计时也跑，但不调度后台采样
        if (Application.Current is not null) _timer.Start();
        Refresh();
    }

    public bool ShowCommSection => string.Equals(Prop("showCommSection", "true"), "true", StringComparison.OrdinalIgnoreCase);
    public bool ShowPlcSection  => string.Equals(Prop("showPlcSection",  "true"), "true", StringComparison.OrdinalIgnoreCase);
    public bool ShowHmiSection  => string.Equals(Prop("showHmiSection",  "true"), "true", StringComparison.OrdinalIgnoreCase);
    public int RefreshIntervalSec
    {
        get
        {
            var raw = Prop("refreshInterval", "1");
            return int.TryParse(raw, out var v) && v > 0 ? v : 1;
        }
    }
    public string Background => Prop("background", "#F8FAFC");

    // 通讯
    [ObservableProperty] private string _opcStatus = "(未知)";
    [ObservableProperty] private bool _opcConnected;
    [ObservableProperty] private int _subscribedTagCount;

    // PLC（先用占位指标，预留扩展）
    [ObservableProperty] private string _plcState = "运行";
    [ObservableProperty] private string _plcErrorCode = "0";

    // HMI 资源
    [ObservableProperty] private double _cpuPercent;
    [ObservableProperty] private double _memoryMb;
    [ObservableProperty] private double _diskFreeGb;

    private void ResolveOpcService()
    {
        var shell = _dataContext.Shell;
        if (shell is null) return;
        var t = shell.GetType();
        var prop = t.GetProperty("OpcUaService", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
        if (prop?.GetValue(shell) is IOpcUaService svc) { _opc = svc; return; }
        var field = t.GetField("_opcUaService", BindingFlags.Instance | BindingFlags.NonPublic);
        if (field?.GetValue(shell) is IOpcUaService svc2) _opc = svc2;
    }

    private void TryInitPerf()
    {
        // CPU 自算（跨平台，不依赖 PerformanceCounter 类别）；diskCounter 不需要
        // 这里保留 _cpuCounter 字段是兼容历史；当前实现走 Process.TotalProcessorTime
    }

    private void Refresh()
    {
        if (_opc is not null)
        {
            OpcConnected = _opc.IsConnected;
            OpcStatus = _opc.ConnectionStatus ?? (_opc.IsConnected ? "已连接" : "未连接");
            SubscribedTagCount = _opc.SubscribedTagNames?.Count ?? 0;
        }
        else
        {
            OpcStatus = "(无 Shell)";
            OpcConnected = false;
            SubscribedTagCount = 0;
        }

        // PLC 状态：暂以 OPC 是否在线 + 订阅数判断（第一版极简）
        if (OpcConnected) { PlcState = SubscribedTagCount > 0 ? "运行" : "在线"; PlcErrorCode = "0"; }
        else { PlcState = "离线"; PlcErrorCode = "-"; }

        // HMI 资源：CPU = ΔProcessTime / Δwall * 核心数倒数
        try
        {
            var proc = Process.GetCurrentProcess();
            var now = DateTime.Now;
            var cpuNow = proc.TotalProcessorTime;
            var dt = (now - _lastSampleAt).TotalMilliseconds;
            if (dt > 50)
            {
                var dcpu = (cpuNow - _lastProcessCpu).TotalMilliseconds;
                var cores = Environment.ProcessorCount;
                CpuPercent = Math.Round(Math.Min(100, dcpu / dt / cores * 100.0), 1);
                _lastProcessCpu = cpuNow;
                _lastSampleAt = now;
            }
            MemoryMb = Math.Round(proc.WorkingSet64 / 1024.0 / 1024.0, 1);
        }
        catch { /* 取不到忽略 */ }

        try
        {
            var root = System.IO.Path.GetPathRoot(AppDomain.CurrentDomain.BaseDirectory);
            if (!string.IsNullOrEmpty(root))
            {
                var di = new System.IO.DriveInfo(root);
                DiskFreeGb = Math.Round(di.AvailableFreeSpace / 1024.0 / 1024.0 / 1024.0, 1);
            }
        }
        catch { /* 取不到忽略 */ }
    }

    public void Dispose()
    {
        _timer.Stop();
        _cpuCounter?.Dispose();
    }
}
