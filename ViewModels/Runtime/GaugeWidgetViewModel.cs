#nullable enable
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using ApexHMI.Models.RuntimeUi;
using ApexHMI.Services.RuntimeUi;

namespace ApexHMI.ViewModels.Runtime;

/// <summary>
/// 量规（Gauge）：简化版半圆弧 + 中心数值。
/// 第一版不画完整圆盘指针，使用弧形进度环 + 中心数值 + 单位。
/// </summary>
public partial class GaugeWidgetViewModel : WidgetViewModelBase
{
    [ObservableProperty] private double _value;

    public GaugeWidgetViewModel(WidgetInstance model, IWidgetDataContext dataContext)
        : base(model, dataContext)
    {
        var tag = ResolveTag();
        if (!string.IsNullOrWhiteSpace(tag))
            dataContext.RegisterValueCallback(tag!, OnTagValueChanged);
    }

    public string MinValueRaw       => Prop("minValue",        "0");
    public string MaxValueRaw       => Prop("maxValue",        "100");
    public string Unit              => Prop("unit",            "");
    public string WarnThresholdRaw  => Prop("warnThreshold",   "");
    public string WarnColor         => Prop("warnColor",       "#F59E0B");
    public string AlarmThresholdRaw => Prop("alarmThreshold",  "");
    public string AlarmColor        => Prop("alarmColor",      "#EF4444");
    public string StartAngleRaw     => Prop("startAngle",      "-135");
    public string EndAngleRaw       => Prop("endAngle",        "135");
    public string MajorTicksRaw     => Prop("majorTicks",      "10");
    public string MinorTicksRaw     => Prop("minorTicks",      "5");
    public string NormalColor       => Prop("foreground",      "#2563EB");

    public double MinValue   => ParseD(MinValueRaw, 0);
    public double MaxValue   => ParseD(MaxValueRaw, 100);
    public double StartAngle => ParseD(StartAngleRaw, -135);
    public double EndAngle   => ParseD(EndAngleRaw, 135);

    public double Ratio
    {
        get
        {
            var min = MinValue;
            var max = MaxValue;
            if (max <= min) return 0;
            var r = (Value - min) / (max - min);
            return r < 0 ? 0 : r > 1 ? 1 : r;
        }
    }

    /// <summary>B2C: useLimitBandColors=true 时启用 5 级带 + hysteresis 防抖。</summary>
    public bool UseLimitBandColors
        => string.Equals(Prop("useLimitBandColors", "false"), "true", System.StringComparison.OrdinalIgnoreCase);

    private string? _lastBandColor;

    public string CurrentColor
    {
        get
        {
            if (UseLimitBandColors)
            {
                var bands = LimitBands.FromProperties(Model.Properties);
                var c = bands.SelectColor(Value, _lastBandColor);
                _lastBandColor = c;
                return c;
            }
            if (double.TryParse(AlarmThresholdRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var alarm) && Value >= alarm)
                return AlarmColor;
            if (double.TryParse(WarnThresholdRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var warn) && Value >= warn)
                return WarnColor;
            return NormalColor;
        }
    }

    public string DisplayText
    {
        get
        {
            var v = Value.ToString("0.##", CultureInfo.InvariantCulture);
            return string.IsNullOrEmpty(Unit) ? v : $"{v} {Unit}";
        }
    }

    private string? ResolveTag()
    {
        var v = Prop("variable", "");
        if (!string.IsNullOrWhiteSpace(v)) return v;
        return Model.Binding?.TagId;
    }

    protected override void OnTagValueChanged(string rawValue)
    {
        if (double.TryParse(rawValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
            Value = v;
    }

    partial void OnValueChanged(double value)
    {
        OnPropertyChanged(nameof(Ratio));
        OnPropertyChanged(nameof(CurrentColor));
        OnPropertyChanged(nameof(DisplayText));
    }

    private static double ParseD(string s, double fallback)
        => double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : fallback;
}
