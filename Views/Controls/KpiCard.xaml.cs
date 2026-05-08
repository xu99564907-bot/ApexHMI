using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ApexHMI.Views.Controls;

/// <summary>
/// Phase 3 KPI 卡复用组件。用法：
///   <controls:KpiCard Tag="OEE" Label="设备综合效率" Value="76.2" Unit="%" Hint="近 30 min 趋势"/>
/// </summary>
public partial class KpiCard : UserControl
{
    public KpiCard() => InitializeComponent();

    public static readonly DependencyProperty TagProperty =
        DependencyProperty.Register(nameof(Tag), typeof(string), typeof(KpiCard), new PropertyMetadata("KPI"));
    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(KpiCard), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(string), typeof(KpiCard), new PropertyMetadata("--"));
    public static readonly DependencyProperty UnitProperty =
        DependencyProperty.Register(nameof(Unit), typeof(string), typeof(KpiCard), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty HintProperty =
        DependencyProperty.Register(nameof(Hint), typeof(string), typeof(KpiCard), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty AccentBrushProperty =
        DependencyProperty.Register(nameof(AccentBrush), typeof(Brush), typeof(KpiCard),
            new PropertyMetadata((Brush)new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB))));

    public new string Tag { get => (string)GetValue(TagProperty); set => SetValue(TagProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string Value { get => (string)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public string Unit { get => (string)GetValue(UnitProperty); set => SetValue(UnitProperty, value); }
    public string Hint { get => (string)GetValue(HintProperty); set => SetValue(HintProperty, value); }
    public Brush AccentBrush { get => (Brush)GetValue(AccentBrushProperty); set => SetValue(AccentBrushProperty, value); }
}
