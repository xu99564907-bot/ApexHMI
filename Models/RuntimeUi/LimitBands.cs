#nullable enable
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ApexHMI.Models.RuntimeUi;

/// <summary>
/// B2C: WinCC 5 级限值带 (Bar / Gauge / Trend)。
/// <para>结构：alarmLow &lt; warningLow &lt; toleranceLow &lt;= toleranceHigh &lt; warningHigh &lt; alarmHigh</para>
/// <para>颜色映射：</para>
/// <list type="bullet">
///   <item>Tolerance 区间内（≥ toleranceLow 且 ≤ toleranceHigh）→ NormalColor（常态蓝）。</item>
///   <item>Warning 区间（toleranceHigh ~ warningHigh 或 warningLow ~ toleranceLow）→ WarningColor 黄。</item>
///   <item>Alarm 区间（&gt; warningHigh 或 &lt; warningLow）→ AlarmColor 红。</item>
/// </list>
/// <para>colorChangeHysteresis：防抖，值在阈值附近反复时不切色。</para>
/// </summary>
public partial class LimitBands : ObservableObject
{
    [ObservableProperty] private double _alarmHigh = 100;
    [ObservableProperty] private double _warningHigh = 90;
    [ObservableProperty] private double _toleranceHigh = 80;
    [ObservableProperty] private double _toleranceLow = 20;
    [ObservableProperty] private double _warningLow = 10;
    [ObservableProperty] private double _alarmLow = 0;

    [ObservableProperty] private string _alarmColor = "#DC2626";
    [ObservableProperty] private string _warningColor = "#F59E0B";
    [ObservableProperty] private string _toleranceColor = "#22C55E";
    [ObservableProperty] private string _normalColor = "#3B82F6";

    [ObservableProperty] private double _colorChangeHysteresis = 0;

    /// <summary>
    /// B2C: 根据值与上一次的色，按 hysteresis 防抖切色。
    /// 算法：候选色由值落在哪个区间确定；若候选与上次同 → 不变；
    /// 否则需要"额外越界 hysteresis"才切（防止 epsilon 抖动）。
    /// </summary>
    public string SelectColor(double value, string? previousColor)
    {
        var candidate = SelectColorRaw(value);
        if (ColorChangeHysteresis <= 0 || string.IsNullOrEmpty(previousColor) || candidate == previousColor)
            return candidate;

        // 找出"切换需要的边界"。简化：若值正好落在阈值附近 hysteresis 内则保持旧色。
        var nearest = NearestThresholdDistance(value);
        if (nearest < ColorChangeHysteresis) return previousColor;
        return candidate;
    }

    /// <summary>不带防抖的纯区间映射。</summary>
    public string SelectColorRaw(double value)
    {
        if (value > AlarmHigh)     return AlarmColor;
        if (value > WarningHigh)   return AlarmColor;
        if (value > ToleranceHigh) return WarningColor;
        if (value >= ToleranceLow) return NormalColor;
        if (value >= WarningLow)   return WarningColor;
        if (value >= AlarmLow)     return AlarmColor;
        return AlarmColor;
    }

    /// <summary>返回 value 距最近阈值的距离（用于 hysteresis 判定）。</summary>
    private double NearestThresholdDistance(double value)
    {
        double best = double.MaxValue;
        foreach (var t in new[] { AlarmHigh, WarningHigh, ToleranceHigh, ToleranceLow, WarningLow, AlarmLow })
        {
            var d = System.Math.Abs(value - t);
            if (d < best) best = d;
        }
        return best;
    }

    /// <summary>从 WidgetInstance.Properties 读 6 阈值 + 4 色 + hysteresis 构造。</summary>
    public static LimitBands FromProperties(System.Collections.Generic.IReadOnlyDictionary<string, string> props)
    {
        double Get(string k, double d)
            => props.TryGetValue(k, out var v) && double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var x) ? x : d;
        string Color(string k, string d) => props.TryGetValue(k, out var v) && !string.IsNullOrWhiteSpace(v) ? v : d;

        return new LimitBands
        {
            AlarmHigh             = Get("alarmHigh",     100),
            WarningHigh           = Get("warningHigh",   90),
            ToleranceHigh         = Get("toleranceHigh", 80),
            ToleranceLow          = Get("toleranceLow",  20),
            WarningLow            = Get("warningLow",    10),
            AlarmLow              = Get("alarmLow",      0),
            AlarmColor            = Color("alarmHighColor",   "#DC2626"),
            WarningColor          = Color("warningHighColor", "#F59E0B"),
            ToleranceColor        = Color("toleranceColor",   "#22C55E"),
            NormalColor           = Color("normalColor",      "#3B82F6"),
            ColorChangeHysteresis = Get("colorChangeHysteresis", 0),
        };
    }
}
