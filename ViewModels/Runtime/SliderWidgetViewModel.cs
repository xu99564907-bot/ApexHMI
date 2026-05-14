#nullable enable
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ApexHMI.Models.RuntimeUi;
using ApexHMI.Services.RuntimeUi;

namespace ApexHMI.ViewModels.Runtime;

/// <summary>
/// 滑块（Slider）：拖动改变 Value，writeOnChange=true 时拖动即写，否则释放时写。
/// </summary>
public partial class SliderWidgetViewModel : WidgetViewModelBase
{
    [ObservableProperty] private double _value;
    private bool _initializingFromTag;

    public SliderWidgetViewModel(WidgetInstance model, IWidgetDataContext dataContext)
        : base(model, dataContext)
    {
        var tag = ResolveTag();
        if (!string.IsNullOrWhiteSpace(tag))
            dataContext.RegisterValueCallback(tag!, OnTagValueChanged);
    }

    public string MinValueRaw      => Prop("minValue",      "0");
    public string MaxValueRaw      => Prop("maxValue",      "100");
    public string StepRaw          => Prop("step",          "1");
    public string OrientationProp  => Prop("orientation",   "horizontal");
    public string ShowLabelRaw     => Prop("showLabel",     "false");
    public string ShowValueRaw     => Prop("showValue",     "true");
    public string SnapToStepRaw    => Prop("snapToStep",    "true");
    public string WriteOnChangeRaw => Prop("writeOnChange", "false");

    public double MinValue => ParseD(MinValueRaw, 0);
    public double MaxValue => ParseD(MaxValueRaw, 100);
    public double Step     => ParseD(StepRaw, 1);

    public bool ShowValue     => string.Equals(ShowValueRaw,    "true", System.StringComparison.OrdinalIgnoreCase);
    public bool SnapToStep    => string.Equals(SnapToStepRaw,   "true", System.StringComparison.OrdinalIgnoreCase);
    public bool WriteOnChange => string.Equals(WriteOnChangeRaw,"true", System.StringComparison.OrdinalIgnoreCase);

    // B3.2: WinCC Slider 扩展
    public double SmallChange => ParseD(Prop("smallChange", StepRaw), 1);
    public double LargeChange => ParseD(Prop("largeChange", "10"), 10);
    public bool ShowTicks     => string.Equals(Prop("showTicks", "false"), "true", System.StringComparison.OrdinalIgnoreCase);
    public double TickFrequency => ParseD(Prop("tickFrequency", "10"), 10);
    public bool IsDirectionReversed =>
        string.Equals(Prop("direction", "Normal"), "Reversed", System.StringComparison.OrdinalIgnoreCase);

    public System.Windows.Controls.Primitives.TickPlacement TickPlacementEnum =>
        Prop("tickPlacement", ShowTicks ? "BottomRight" : "None").ToLowerInvariant() switch
        {
            "none" => System.Windows.Controls.Primitives.TickPlacement.None,
            "topleft" => System.Windows.Controls.Primitives.TickPlacement.TopLeft,
            "both" => System.Windows.Controls.Primitives.TickPlacement.Both,
            _ => ShowTicks ? System.Windows.Controls.Primitives.TickPlacement.BottomRight
                           : System.Windows.Controls.Primitives.TickPlacement.None,
        };

    public System.Windows.Controls.Orientation Orientation =>
        string.Equals(OrientationProp, "vertical", System.StringComparison.OrdinalIgnoreCase)
            ? System.Windows.Controls.Orientation.Vertical
            : System.Windows.Controls.Orientation.Horizontal;

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
            _initializingFromTag = true;
            try { Value = v; }
            finally { _initializingFromTag = false; }
        }
    }

    partial void OnValueChanged(double value)
    {
        OnPropertyChanged(nameof(DisplayText));
        if (_initializingFromTag) return;
        if (WriteOnChange) WriteValue();
    }

    /// <summary>从 UI（Slider Thumb 释放）触发写回 PLC。</summary>
    [RelayCommand]
    public void Commit() => WriteValue();

    private void WriteValue()
    {
        var tag = ResolveTag();
        if (string.IsNullOrWhiteSpace(tag)) return;

        var isInt = Step >= 1 && Step == System.Math.Floor(Step);
        if (isInt)
            _dataContext.ExecuteAction("write-int", $"{tag}|{(long)System.Math.Round(Value)}");
        else
            _dataContext.ExecuteAction("write-float", $"{tag}|{Value.ToString(CultureInfo.InvariantCulture)}");
    }

    private static double ParseD(string s, double fallback)
        => double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : fallback;
}
