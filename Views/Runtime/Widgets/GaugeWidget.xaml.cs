using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using ApexHMI.ViewModels.Runtime;

namespace ApexHMI.Views.Runtime.Widgets;

public partial class GaugeWidget : UserControl
{
    private GaugeWidgetViewModel? _vm;

    public GaugeWidget()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => HookVm();
        SizeChanged += (_, _) => Redraw();
    }

    private void HookVm()
    {
        if (_vm is not null) _vm.PropertyChanged -= OnVmChanged;
        _vm = DataContext as GaugeWidgetViewModel;
        if (_vm is not null) _vm.PropertyChanged += OnVmChanged;
        Redraw();
    }

    private void OnVmChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(GaugeWidgetViewModel.Ratio)
                          or nameof(GaugeWidgetViewModel.CurrentColor))
            Redraw();
    }

    private void Redraw()
    {
        ArcCanvas.Children.Clear();
        if (_vm is null) return;
        if (ActualWidth <= 0 || ActualHeight <= 0) return;

        var w = ActualWidth;
        var h = ActualHeight;
        var cx = w / 2;
        var cy = h * 0.85; // 半圆弧靠下，给数字留居中空间
        var radius = Math.Min(w / 2, h * 0.85) - 8;
        if (radius <= 0) return;

        var start = _vm.StartAngle;
        var end = _vm.EndAngle;
        var ratio = _vm.Ratio;
        var current = start + (end - start) * ratio;

        // 背景弧
        ArcCanvas.Children.Add(BuildArc(cx, cy, radius, start, end,
            new SolidColorBrush(Color.FromRgb(0xE5, 0xE7, 0xEB)), 6));

        // 进度弧
        Brush fillBrush;
        try { fillBrush = (Brush)new BrushConverter().ConvertFromString(_vm.CurrentColor)!; }
        catch { fillBrush = Brushes.SteelBlue; }
        ArcCanvas.Children.Add(BuildArc(cx, cy, radius, start, current, fillBrush, 8));
    }

    private static Path BuildArc(double cx, double cy, double r, double startDeg, double endDeg, Brush stroke, double thickness)
    {
        var startRad = (startDeg - 90) * Math.PI / 180.0;
        var endRad = (endDeg - 90) * Math.PI / 180.0;
        var startPt = new Point(cx + r * Math.Cos(startRad), cy + r * Math.Sin(startRad));
        var endPt   = new Point(cx + r * Math.Cos(endRad),   cy + r * Math.Sin(endRad));
        var isLarge = Math.Abs(endDeg - startDeg) > 180;
        var sweep = endDeg >= startDeg ? SweepDirection.Clockwise : SweepDirection.Counterclockwise;

        var fig = new PathFigure { StartPoint = startPt };
        fig.Segments.Add(new ArcSegment(endPt, new Size(r, r), 0, isLarge, sweep, true));
        var geo = new PathGeometry();
        geo.Figures.Add(fig);
        return new Path
        {
            Data = geo,
            Stroke = stroke,
            StrokeThickness = thickness,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        };
    }
}
