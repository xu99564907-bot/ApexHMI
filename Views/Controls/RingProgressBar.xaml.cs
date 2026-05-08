using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ApexHMI.Views.Controls;

/// <summary>
/// Phase 3 通用环形进度组件。用法：
///   <controls:RingProgressBar Diameter="120" Value="56.9" Caption="目标完成率"/>
/// 不依赖外部 binding，纯自渲染；颜色随 Value 自动切档（Danger / Warning / Success）。
/// </summary>
public partial class RingProgressBar : UserControl
{
    public RingProgressBar()
    {
        InitializeComponent();
    }

    public static readonly DependencyProperty DiameterProperty =
        DependencyProperty.Register(nameof(Diameter), typeof(double), typeof(RingProgressBar),
            new PropertyMetadata(120d, OnVisualChanged));

    public static readonly DependencyProperty ThicknessProperty =
        DependencyProperty.Register(nameof(Thickness), typeof(double), typeof(RingProgressBar),
            new PropertyMetadata(10d, OnVisualChanged));

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(double), typeof(RingProgressBar),
            new PropertyMetadata(0d, OnVisualChanged));

    public static readonly DependencyProperty CaptionProperty =
        DependencyProperty.Register(nameof(Caption), typeof(string), typeof(RingProgressBar),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ValueFontSizeProperty =
        DependencyProperty.Register(nameof(ValueFontSize), typeof(double), typeof(RingProgressBar),
            new PropertyMetadata(22d));

    public static readonly DependencyProperty TrackBrushProperty =
        DependencyProperty.Register(nameof(TrackBrush), typeof(Brush), typeof(RingProgressBar),
            new PropertyMetadata((Brush)new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0))));

    public static readonly DependencyProperty ArcBrushProperty =
        DependencyProperty.Register(nameof(ArcBrush), typeof(Brush), typeof(RingProgressBar),
            new PropertyMetadata((Brush)new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB))));

    public static readonly DependencyProperty ValueBrushProperty =
        DependencyProperty.Register(nameof(ValueBrush), typeof(Brush), typeof(RingProgressBar),
            new PropertyMetadata((Brush)new SolidColorBrush(Color.FromRgb(0x0F, 0x17, 0x2A))));

    public static readonly DependencyProperty PercentTextProperty =
        DependencyProperty.Register(nameof(PercentText), typeof(string), typeof(RingProgressBar),
            new PropertyMetadata("0%"));

    public static readonly DependencyProperty ArcGeometryProperty =
        DependencyProperty.Register(nameof(ArcGeometry), typeof(Geometry), typeof(RingProgressBar),
            new PropertyMetadata(Geometry.Empty));

    public double Diameter { get => (double)GetValue(DiameterProperty); set => SetValue(DiameterProperty, value); }
    public double Thickness { get => (double)GetValue(ThicknessProperty); set => SetValue(ThicknessProperty, value); }
    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public string Caption { get => (string)GetValue(CaptionProperty); set => SetValue(CaptionProperty, value); }
    public double ValueFontSize { get => (double)GetValue(ValueFontSizeProperty); set => SetValue(ValueFontSizeProperty, value); }
    public Brush TrackBrush { get => (Brush)GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }
    public Brush ArcBrush { get => (Brush)GetValue(ArcBrushProperty); set => SetValue(ArcBrushProperty, value); }
    public Brush ValueBrush { get => (Brush)GetValue(ValueBrushProperty); set => SetValue(ValueBrushProperty, value); }
    public string PercentText { get => (string)GetValue(PercentTextProperty); set => SetValue(PercentTextProperty, value); }
    public Geometry ArcGeometry { get => (Geometry)GetValue(ArcGeometryProperty); set => SetValue(ArcGeometryProperty, value); }

    private static void OnVisualChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((RingProgressBar)d).Recompute();

    private void Recompute()
    {
        var pct = Math.Max(0, Math.Min(100, Value));
        PercentText = pct.ToString("0.0", CultureInfo.InvariantCulture) + "%";

        // Arc geometry: from top (-90°) clockwise by `pct/100 * 360°`
        var d = Diameter;
        var t = Thickness;
        if (d <= 0 || t <= 0) { ArcGeometry = Geometry.Empty; return; }
        var radius = (d - t) / 2;
        var center = new Point(d / 2, d / 2);
        var startAngle = -90.0;
        var sweep = pct / 100.0 * 360.0;
        var endAngle = startAngle + sweep;
        if (sweep <= 0.001) { ArcGeometry = Geometry.Empty; return; }

        var startPoint = AngleToPoint(center, radius, startAngle);
        var endPoint = AngleToPoint(center, radius, endAngle);
        var isLargeArc = sweep > 180;

        var fig = new PathFigure { StartPoint = startPoint, IsClosed = false };
        fig.Segments.Add(new ArcSegment(endPoint,
            new Size(radius, radius), 0, isLargeArc, SweepDirection.Clockwise, true));
        var geom = new PathGeometry();
        geom.Figures.Add(fig);
        ArcGeometry = geom;

        // 自动切色：>=80 success，>=50 primary，<50 warning，<25 danger
        ArcBrush = pct >= 80 ? new SolidColorBrush(Color.FromRgb(0x0F, 0x76, 0x6E))
                 : pct >= 50 ? new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB))
                 : pct >= 25 ? new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B))
                 :              new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26));
    }

    private static Point AngleToPoint(Point center, double r, double angleDeg)
    {
        var a = angleDeg * Math.PI / 180.0;
        return new Point(center.X + r * Math.Cos(a), center.Y + r * Math.Sin(a));
    }
}
