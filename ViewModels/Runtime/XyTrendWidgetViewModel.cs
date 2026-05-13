#nullable enable
using System;
using System.Globalization;
using System.Windows;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using ApexHMI.Models.RuntimeUi;
using ApexHMI.Services.RuntimeUi;

namespace ApexHMI.ViewModels.Runtime;

/// <summary>
/// P9E: f(x) XY 趋势 — 散点 + 连线，X/Y 各绑定一个 Tag。
/// 两个 Tag 任一变化时，用各自最近值组合追加一个点（滚动 MaxPoints）。
/// </summary>
public partial class XyTrendWidgetViewModel : WidgetViewModelBase
{
    private readonly ScatterSeries? _scatter;
    private readonly LineSeries? _line;
    private double? _lastX;
    private double? _lastY;

    public XyTrendWidgetViewModel(WidgetInstance model, IWidgetDataContext dataContext)
        : base(model, dataContext)
    {
        PlotModel = BuildPlotModel(out _scatter, out _line);

        if (!string.IsNullOrWhiteSpace(XVariable))
            dataContext.RegisterValueCallback(XVariable, OnXPushed);
        if (!string.IsNullOrWhiteSpace(YVariable))
            dataContext.RegisterValueCallback(YVariable, OnYPushed);

        if (IsDesignTime()) SeedDemoData();
    }

    public PlotModel PlotModel { get; }

    public string XVariable => Prop("xVariable", "");
    public string YVariable => Prop("yVariable", "");
    public string Mode      => Prop("mode", "Scatter"); // Scatter / Line / Both
    public string XLabel    => Prop("xLabel", "X");
    public string YLabel    => Prop("yLabel", "Y");
    public string XMinRaw   => Prop("xMin", "auto");
    public string XMaxRaw   => Prop("xMax", "auto");
    public string YMinRaw   => Prop("yMin", "auto");
    public string YMaxRaw   => Prop("yMax", "auto");
    public string ColorHex  => Prop("color", "#2563EB");

    public int MaxPoints
    {
        get
        {
            if (int.TryParse(Prop("maxPoints", "200"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n > 0)
                return n;
            return 200;
        }
    }

    private PlotModel BuildPlotModel(out ScatterSeries? scatter, out LineSeries? line)
    {
        var pm = new PlotModel
        {
            PlotAreaBorderColor = OxyColor.FromArgb(255, 148, 163, 184),
            Padding = new OxyThickness(4),
        };

        var xa = new LinearAxis { Position = AxisPosition.Bottom, Title = XLabel };
        var ya = new LinearAxis { Position = AxisPosition.Left,   Title = YLabel };
        if (double.TryParse(XMinRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var xmin)) xa.Minimum = xmin;
        if (double.TryParse(XMaxRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var xmax)) xa.Maximum = xmax;
        if (double.TryParse(YMinRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var ymin)) ya.Minimum = ymin;
        if (double.TryParse(YMaxRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var ymax)) ya.Maximum = ymax;
        pm.Axes.Add(xa);
        pm.Axes.Add(ya);

        var color = ParseColor(ColorHex);
        scatter = null;
        line = null;
        var mode = Mode.ToLowerInvariant();
        if (mode == "scatter" || mode == "both")
        {
            scatter = new ScatterSeries { MarkerType = MarkerType.Circle, MarkerSize = 4, MarkerFill = color };
            pm.Series.Add(scatter);
        }
        if (mode == "line" || mode == "both")
        {
            line = new LineSeries { Color = color, StrokeThickness = 1.5, MarkerType = MarkerType.None };
            pm.Series.Add(line);
        }
        // 默认散点
        if (scatter is null && line is null)
        {
            scatter = new ScatterSeries { MarkerType = MarkerType.Circle, MarkerSize = 4, MarkerFill = color };
            pm.Series.Add(scatter);
        }
        return pm;
    }

    private void OnXPushed(string raw)
    {
        if (double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var v)) _lastX = v;
        TryAppend();
    }

    private void OnYPushed(string raw)
    {
        if (double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var v)) _lastY = v;
        TryAppend();
    }

    private void TryAppend()
    {
        if (_lastX is not double x || _lastY is not double y) return;
        if (_scatter is not null)
        {
            _scatter.Points.Add(new ScatterPoint(x, y));
            while (_scatter.Points.Count > MaxPoints) _scatter.Points.RemoveAt(0);
        }
        if (_line is not null)
        {
            _line.Points.Add(new DataPoint(x, y));
            while (_line.Points.Count > MaxPoints) _line.Points.RemoveAt(0);
        }
        if (Application.Current?.Dispatcher is { } d && !d.CheckAccess())
            d.BeginInvoke(new Action(() => PlotModel.InvalidatePlot(true)));
        else
            PlotModel.InvalidatePlot(true);
    }

    private void SeedDemoData()
    {
        // 设计时：渲染圆周散点示意
        for (int i = 0; i < 36; i++)
        {
            double t = i * Math.PI / 18.0;
            double x = 50 + 30 * Math.Cos(t);
            double y = 50 + 30 * Math.Sin(t);
            _scatter?.Points.Add(new ScatterPoint(x, y));
            _line?.Points.Add(new DataPoint(x, y));
        }
        PlotModel.InvalidatePlot(true);
    }

    private bool IsDesignTime()
    {
        var shell = _dataContext.Shell;
        if (shell is null) return true;
        return shell.GetType().Name.Contains("Designer", StringComparison.OrdinalIgnoreCase);
    }

    private static OxyColor ParseColor(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return OxyColors.SteelBlue;
        try
        {
            var c = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
            return OxyColor.FromArgb(c.A, c.R, c.G, c.B);
        }
        catch { return OxyColors.SteelBlue; }
    }
}
