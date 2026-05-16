#nullable enable
using System;
using System.Globalization;
using System.Windows.Threading;
using ApexHMI.Services.DataBinding;
using Serilog;

namespace ApexHMI.Services;

/// <summary>
/// P10F: 离线模拟服务 — 启动后给一组常见 Tag（SimTag1..SimTag5、SimBool1..SimBool3、SimText1）
/// 注入假数据，用于无 PLC / 演示场景：
/// <list type="bullet">
///   <item>SimTag1：正弦 0~100 (1Hz)</item>
///   <item>SimTag2：随机 0~100</item>
///   <item>SimTag3：阶梯递增</item>
///   <item>SimTag4：方波 0/100</item>
///   <item>SimTag5：三角波 0~100</item>
///   <item>SimBool1..3：每 3s 翻转</item>
///   <item>SimText1：随机文本</item>
/// </list>
/// </summary>
public sealed class SimulationService
{
    private readonly RuntimeDataBindingService _dataBinding;
    private DispatcherTimer? _timer;
    private readonly Random _rand = new();
    private double _t;
    private int _stepCounter;
    private bool _bool1, _bool2, _bool3;
    private static readonly string[] _texts = { "运行中", "待机", "故障", "准备", "完成" };

    public SimulationService(RuntimeDataBindingService dataBinding)
    {
        _dataBinding = dataBinding;
    }

    public bool IsRunning { get; private set; }

    public void Start()
    {
        if (IsRunning) return;
        _t = 0;
        _stepCounter = 0;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _timer.Tick += OnTick;
        _timer.Start();
        IsRunning = true;
        Log.Information("SimulationService: 已启动离线模拟（注入 SimTag1..5 / SimBool1..3 / SimText1）");
    }

    public void Stop()
    {
        if (!IsRunning) return;
        _timer?.Stop();
        _timer = null;
        IsRunning = false;
        Log.Information("SimulationService: 已停止");
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _t += 0.2;
        _stepCounter++;

        var sine = (Math.Sin(_t) + 1) * 50; // 0~100
        var rnd = _rand.NextDouble() * 100;
        var step = (_stepCounter % 100);
        var square = ((int)(_t / 2) % 2 == 0) ? 100 : 0;
        var tri = Math.Abs(((_t * 10) % 200) - 100);

        _dataBinding.PushSimulatedValue("SimTag1", sine.ToString("F2", CultureInfo.InvariantCulture));
        _dataBinding.PushSimulatedValue("SimTag2", rnd.ToString("F2", CultureInfo.InvariantCulture));
        _dataBinding.PushSimulatedValue("SimTag3", step.ToString(CultureInfo.InvariantCulture));
        _dataBinding.PushSimulatedValue("SimTag4", square.ToString(CultureInfo.InvariantCulture));
        _dataBinding.PushSimulatedValue("SimTag5", tri.ToString("F2", CultureInfo.InvariantCulture));

        // 每 15 个 tick (~3s) 翻转 bool
        if (_stepCounter % 15 == 0)
        {
            _bool1 = !_bool1;
            _bool2 = _rand.NextDouble() > 0.5;
            _bool3 = !_bool3;
            _dataBinding.PushSimulatedValue("SimBool1", _bool1 ? "True" : "False");
            _dataBinding.PushSimulatedValue("SimBool2", _bool2 ? "True" : "False");
            _dataBinding.PushSimulatedValue("SimBool3", _bool3 ? "True" : "False");
            _dataBinding.PushSimulatedValue("SimText1", _texts[_rand.Next(_texts.Length)]);
        }
    }
}
