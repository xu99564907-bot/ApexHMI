#nullable enable
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using ApexHMI.Models.RuntimeUi;
using ApexHMI.Services.RuntimeUi;

namespace ApexHMI.ViewModels.Runtime;

/// <summary>
/// 棒图（Bar）：垂直 / 水平，根据 value 在 [min,max] 区间内的比例显示填充长度。
/// 阈值变色：value > warnThreshold 用 warnColor；value > alarmThreshold 用 alarmColor。
/// </summary>
public partial class BarWidgetViewModel : WidgetViewModelBase
{
    [ObservableProperty] private double _value;

    public BarWidgetViewModel(WidgetInstance model, IWidgetDataContext dataContext)
        : base(model, dataContext)
    {
        var tag = ResolveTag();
        if (!string.IsNullOrWhiteSpace(tag))
            dataContext.RegisterValueCallback(tag!, OnTagValueChanged);
    }

    public string MinValueRaw      => Prop("minValue",         "0");
    public string MaxValueRaw      => Prop("maxValue",         "100");
    public string OrientationProp  => Prop("orientation",      "vertical");
    public string FillColor        => Prop("fillColor",        "#3B82F6");
    public string BackgroundColor  => Prop("backgroundColor",  "#E5E7EB");
    public string WarnThresholdRaw => Prop("warnThreshold",    "");
    public string WarnColor        => Prop("warnColor",        "#F59E0B");
    public string AlarmThresholdRaw=> Prop("alarmThreshold",   "");
    public string AlarmColor       => Prop("alarmColor",       "#EF4444");
    public string ShowLabelRaw     => Prop("showLabel",        "true");
    public string ShowScaleRaw     => Prop("showScale",        "false");
    public string ScaleDivisionsRaw=> Prop("scaleDivisions",   "5");

    public double MinValue => ParseD(MinValueRaw, 0);
    public double MaxValue => ParseD(MaxValueRaw, 100);

    public bool ShowLabel => string.Equals(ShowLabelRaw, "true", System.StringComparison.OrdinalIgnoreCase);
    public bool ShowScale => string.Equals(ShowScaleRaw, "true", System.StringComparison.OrdinalIgnoreCase);

    public bool IsVertical => string.Equals(OrientationProp, "vertical", System.StringComparison.OrdinalIgnoreCase);

    public string CurrentFillColor
    {
        get
        {
            if (double.TryParse(AlarmThresholdRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var alarm) && Value >= alarm)
                return AlarmColor;
            if (double.TryParse(WarnThresholdRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var warn) && Value >= warn)
                return WarnColor;
            return FillColor;
        }
    }

    /// <summary>归一化比例 0~1。</summary>
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

    public string DisplayText => Value.ToString("0.##", CultureInfo.InvariantCulture);

    private string? ResolveTag()
    {
        var v = Prop("variable", "");
        if (!string.IsNullOrWhiteSpace(v)) return v;
        return Model.Binding?.TagId;
    }

    protected override void OnTagValueChanged(string rawValue)
    {
        if (double.TryParse(rawValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
        {
            Value = v;
        }
    }

    partial void OnValueChanged(double value)
    {
        OnPropertyChanged(nameof(Ratio));
        OnPropertyChanged(nameof(CurrentFillColor));
        OnPropertyChanged(nameof(DisplayText));
    }

    private static double ParseD(string s, double fallback)
        => double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : fallback;
}
