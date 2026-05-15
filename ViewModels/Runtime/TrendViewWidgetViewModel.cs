#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;
using ApexHMI.Models.RuntimeUi;
using ApexHMI.Services.RuntimeUi;
using Microsoft.Win32;

namespace ApexHMI.ViewModels.Runtime;

/// <summary>
/// M4.1 重做：TrendView 多 Y 轴 + Ruler 光标 + 完整工具栏。
/// <list type="bullet">
///   <item>最多 4 Y 轴（左 2 / 右 2），按 schema yAxisCount 决定显示数</item>
///   <item>每条 trace 的 yAxisIndex 决定挂哪条 Y 轴</item>
///   <item>各 Y 轴独立 Min/Max/Color/Title/Scale (Linear/Logarithmic)</item>
///   <item>Ruler：View 层鼠标 hover → VM 计算每条曲线在 X 时刻的最近数据点 → popup 表显示</item>
///   <item>工具栏：Zoom +/-/Reset / Pause/Resume / Export CSV / Export PNG，按 schema 显隐</item>
///   <item>Pause 时不 append 新点；CSV 导出按当前可见时间范围</item>
/// </list>
/// </summary>
public partial class TrendViewWidgetViewModel : WidgetViewModelBase
{
    private const int MaxPointsPerSeries = 1000;
    private const string YAxisKeyPrefix  = "Y";

    private readonly List<TraceConfig> _traceConfigs = new();
    private readonly Dictionary<string, LineSeries> _seriesByTag = new(StringComparer.OrdinalIgnoreCase);
    private readonly LineAnnotation _rulerLine;

    [ObservableProperty] private bool _paused;
    [ObservableProperty] private bool _rulerVisible;
    [ObservableProperty] private string _rulerTimeText = string.Empty;

    public ObservableCollection<RulerRow> RulerRows { get; } = new();

    public TrendViewWidgetViewModel(WidgetInstance model, IWidgetDataContext dataContext)
        : base(model, dataContext)
    {
        PlotModel = new PlotModel
        {
            PlotAreaBorderColor = OxyColor.FromArgb(255, 148, 163, 184),
            Padding = new OxyThickness(4),
        };

        _rulerLine = new LineAnnotation
        {
            Type = LineAnnotationType.Vertical,
            Color = ParseColor(RulerColor),
            StrokeThickness = 1,
            LineStyle = LineStyle.Dash,
            X = double.NaN,
            ClipByYAxis = false,
        };

        BuildXAxis();
        BuildYAxes();
        ParseTraces();
        InitSeries();

        if (ShowRuler)
        {
            PlotModel.Annotations.Add(_rulerLine);
        }

        if (string.Equals(Mode, "realtime", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var cfg in _traceConfigs)
            {
                if (string.IsNullOrWhiteSpace(cfg.TagId)) continue;
                var captured = cfg;
                dataContext.RegisterValueCallback(captured.TagId, raw => OnTagPushed(captured, raw));
            }
            if (IsDesignTime()) SeedDemoData();
        }
        else
        {
            LoadHistoryFromSqlite();
        }

        PlotModel.IsLegendVisible = ShowLegend;
    }

    public PlotModel PlotModel { get; }

    // ========== Schema 字段 ==========

    public string TracesRaw   => Prop("traces", "[]");
    public string Mode        => Prop("mode", "realtime");
    public double TimeWindow  => ParseD(Prop("timeWindow", "60"), 60);
    public bool ShowLegend    => string.Equals(Prop("showLegend", "true"), "true", StringComparison.OrdinalIgnoreCase);
    public bool ShowGrid      => string.Equals(Prop("showGrid", "true"), "true", StringComparison.OrdinalIgnoreCase);
    public bool ShowToolbar   => string.Equals(Prop("showToolbar", "true"), "true", StringComparison.OrdinalIgnoreCase);
    public string BgColor     => Prop("backgroundColor", "#FFFFFF");

    // B2E 多 Y 轴
    public int    YAxisCount  => (int)ParseD(Prop("yAxisCount", "1"), 1);

    // B2E 工具栏开关
    public bool ShowZoom        => string.Equals(Prop("showZoom",        "true"),  "true", StringComparison.OrdinalIgnoreCase);
    public bool ShowExport      => string.Equals(Prop("showExport",      "false"), "true", StringComparison.OrdinalIgnoreCase);
    public bool ShowPauseResume => string.Equals(Prop("showPauseResume", "true"),  "true", StringComparison.OrdinalIgnoreCase);

    // B2E Ruler
    public bool   ShowRuler     => string.Equals(Prop("showRuler", "false"), "true", StringComparison.OrdinalIgnoreCase);
    public string RulerColor    => Prop("rulerColor", "#0F172A");

    // ========== Visibility Helpers ==========

    public Visibility ToolbarVisibility         => ShowToolbar         ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ShowZoomVisibility        => ShowZoom            ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ShowExportVisibility      => ShowExport          ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ShowPauseResumeVisibility => ShowPauseResume     ? Visibility.Visible : Visibility.Collapsed;
    public Visibility RulerPopupVisibility      => RulerVisible        ? Visibility.Visible : Visibility.Collapsed;

    partial void OnRulerVisibleChanged(bool value) => OnPropertyChanged(nameof(RulerPopupVisibility));

    // ========== Build Axes ==========

    private void BuildXAxis()
    {
        PlotModel.Axes.Add(new DateTimeAxis
        {
            Position = AxisPosition.Bottom,
            StringFormat = "HH:mm:ss",
            IntervalType = DateTimeIntervalType.Auto,
            MajorGridlineStyle = ShowGrid ? LineStyle.Dot : LineStyle.None,
            MajorGridlineColor = OxyColor.FromArgb(255, 226, 232, 240),
        });
    }

    /// <summary>M4.1: 创建 1-4 个 Y 轴，按 schema 配置 Min/Max/Color/Title/Scale。</summary>
    private void BuildYAxes()
    {
        var n = Math.Max(1, Math.Min(4, YAxisCount));
        for (int i = 1; i <= n; i++)
        {
            // 1,2 左；3,4 右
            var pos = i <= 2 ? AxisPosition.Left : AxisPosition.Right;
            var key = YAxisKey(i);
            var color = ParseColor(PropAxis(i, "Color", DefaultAxisColor(i)));
            var title = PropAxis(i, "Title", string.Empty);
            var scale = PropAxis(i, "Scale", "Linear");
            var min   = PropAxis(i, "Min", "");
            var max   = PropAxis(i, "Max", "");

            Axis ax = string.Equals(scale, "Logarithmic", StringComparison.OrdinalIgnoreCase)
                ? new LogarithmicAxis()
                : new LinearAxis();
            ax.Position = pos;
            ax.Key = key;
            ax.Title = title;
            ax.TitleColor = color;
            ax.TextColor  = color;
            ax.AxislineColor = color;
            ax.TicklineColor = color;
            ax.MajorGridlineStyle = ShowGrid && i == 1 ? LineStyle.Dot : LineStyle.None;
            ax.MajorGridlineColor = OxyColor.FromArgb(255, 226, 232, 240);
            if (double.TryParse(min, NumberStyles.Any, CultureInfo.InvariantCulture, out var mn)) ax.Minimum = mn;
            if (double.TryParse(max, NumberStyles.Any, CultureInfo.InvariantCulture, out var mx)) ax.Maximum = mx;
            PlotModel.Axes.Add(ax);
        }
    }

    private static string YAxisKey(int idx) => YAxisKeyPrefix + idx;

    /// <summary>取 y{idx}{Suffix} schema 属性，suffix 应首字母大写：Min/Max/Color/Title/Scale。</summary>
    private string PropAxis(int idx, string suffix, string fallback)
        => Prop($"y{idx}{suffix}", fallback);

    private static string DefaultAxisColor(int idx) => idx switch
    {
        1 => "#1F2937",
        2 => "#DC2626",
        3 => "#22C55E",
        _ => "#F59E0B",
    };

    // ========== Series ==========

    private void ParseTraces()
    {
        _traceConfigs.Clear();
        try
        {
            var arr = JsonSerializer.Deserialize<List<JsonElement>>(TracesRaw);
            if (arr is null) return;
            foreach (var el in arr)
            {
                var c = new TraceConfig
                {
                    TagId     = el.TryGetProperty("tagId", out var t)  ? t.GetString() ?? "" : "",
                    Color     = el.TryGetProperty("color", out var co) ? co.GetString() ?? "#2563EB" : "#2563EB",
                    LineWidth = el.TryGetProperty("lineWidth", out var lw) && lw.ValueKind == JsonValueKind.Number ? lw.GetDouble() : 1.5,
                    Label     = el.TryGetProperty("label", out var lb) ? lb.GetString() ?? "" : "",
                    YAxisIndex= el.TryGetProperty("yAxisIndex", out var yi) && yi.ValueKind == JsonValueKind.Number ? Math.Max(1, Math.Min(4, yi.GetInt32())) : 1,
                };
                if (!string.IsNullOrEmpty(c.TagId)) _traceConfigs.Add(c);
            }
        }
        catch { /* ignore */ }
    }

    private void InitSeries()
    {
        var n = Math.Max(1, Math.Min(4, YAxisCount));
        foreach (var cfg in _traceConfigs)
        {
            var axisIdx = Math.Max(1, Math.Min(n, cfg.YAxisIndex));
            var s = new LineSeries
            {
                Title = string.IsNullOrEmpty(cfg.Label) ? cfg.TagId : cfg.Label,
                Color = ParseColor(cfg.Color),
                StrokeThickness = cfg.LineWidth,
                MarkerType = MarkerType.None,
                YAxisKey = YAxisKey(axisIdx),
            };
            _seriesByTag[cfg.TagId] = s;
            PlotModel.Series.Add(s);
        }
    }

    private void OnTagPushed(TraceConfig cfg, string raw)
    {
        if (Paused) return;
        if (!double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var v)) return;
        if (!_seriesByTag.TryGetValue(cfg.TagId, out var s)) return;

        var x = DateTimeAxis.ToDouble(DateTime.Now);
        s.Points.Add(new DataPoint(x, v));

        // 滚动时间窗
        var cutoff = DateTimeAxis.ToDouble(DateTime.Now.AddSeconds(-TimeWindow));
        while (s.Points.Count > 0 && s.Points[0].X < cutoff) s.Points.RemoveAt(0);
        while (s.Points.Count > MaxPointsPerSeries) s.Points.RemoveAt(0);

        if (Application.Current?.Dispatcher is { } d && !d.CheckAccess())
            d.BeginInvoke(new Action(() => PlotModel.InvalidatePlot(true)));
        else
            PlotModel.InvalidatePlot(true);
    }

    private void LoadHistoryFromSqlite()
    {
        try
        {
            var svc = new ApexHMI.Services.TrendHistoryService();
            var to = DateTime.Now;
            var from = to.AddSeconds(-TimeWindow);
            foreach (var (cfg, series) in _seriesByTag.Select(kv => (cfg: _traceConfigs.FirstOrDefault(c => c.TagId == kv.Key), s: kv.Value)))
            {
                if (cfg is null) continue;
                var pts = svc.Query(cfg.TagId, from, to);
                foreach (var p in pts)
                    series.Points.Add(new DataPoint(DateTimeAxis.ToDouble(p.Time), p.Value));
            }
            PlotModel.InvalidatePlot(true);
        }
        catch { /* ignore */ }
    }

    private void SeedDemoData()
    {
        var s1 = new LineSeries { Title = "demo-sin", Color = OxyColors.SteelBlue, StrokeThickness = 1.5, YAxisKey = YAxisKey(1) };
        var s2 = new LineSeries { Title = "demo-cos", Color = OxyColors.IndianRed, StrokeThickness = 1.5, YAxisKey = YAxisKey(Math.Min(2, Math.Max(1, YAxisCount))) };
        var now = DateTime.Now;
        for (int i = -60; i <= 0; i++)
        {
            var t = DateTimeAxis.ToDouble(now.AddSeconds(i));
            s1.Points.Add(new DataPoint(t, 50 + 40 * Math.Sin(i * 0.18)));
            s2.Points.Add(new DataPoint(t, 50 + 40 * Math.Cos(i * 0.18)));
        }
        PlotModel.Series.Add(s1);
        PlotModel.Series.Add(s2);
        PlotModel.InvalidatePlot(true);
    }

    private bool IsDesignTime()
    {
        var shell = _dataContext.Shell;
        if (shell is null) return true;
        return shell.GetType().Name.Contains("Designer", StringComparison.OrdinalIgnoreCase);
    }

    // ========== Ruler ==========

    /// <summary>M4.1 View 调入：鼠标在 PlotView 上移动时，按 X 数据坐标找各曲线最近点更新 popup。</summary>
    public void UpdateRuler(double xData, double mouseX, double mouseY)
    {
        if (!ShowRuler) return;
        _rulerLine.X = xData;
        try
        {
            RulerTimeText = DateTimeAxis.ToDateTime(xData).ToString("HH:mm:ss.fff");
        }
        catch
        {
            RulerTimeText = string.Empty;
        }

        RulerRows.Clear();
        foreach (var s in PlotModel.Series.OfType<LineSeries>())
        {
            var pt = FindNearestPoint(s, xData);
            var color = OxyColor.FromArgb(s.Color.A, s.Color.R, s.Color.G, s.Color.B);
            RulerRows.Add(new RulerRow
            {
                Label = s.Title,
                ValueText = double.IsNaN(pt.Y) ? "—" : pt.Y.ToString("F2", CultureInfo.InvariantCulture),
                ColorBrush = new SolidColorBrush(Color.FromArgb(color.A, color.R, color.G, color.B)),
            });
        }
        RulerVisible = true;
        PlotModel.InvalidatePlot(false);
    }

    public void HideRuler()
    {
        _rulerLine.X = double.NaN;
        RulerVisible = false;
        PlotModel.InvalidatePlot(false);
    }

    private static DataPoint FindNearestPoint(LineSeries s, double x)
    {
        if (s.Points.Count == 0) return new DataPoint(x, double.NaN);
        DataPoint best = s.Points[0];
        var bestDiff = Math.Abs(best.X - x);
        for (int i = 1; i < s.Points.Count; i++)
        {
            var diff = Math.Abs(s.Points[i].X - x);
            if (diff < bestDiff) { bestDiff = diff; best = s.Points[i]; }
        }
        return best;
    }

    // ========== Commands ==========

    [RelayCommand]
    private void TogglePause() => Paused = !Paused;

    [RelayCommand]
    private void ZoomIn()
    {
        foreach (var ax in PlotModel.Axes) ax.ZoomAtCenter(1.25);
        PlotModel.InvalidatePlot(false);
    }

    [RelayCommand]
    private void ZoomOut()
    {
        foreach (var ax in PlotModel.Axes) ax.ZoomAtCenter(0.8);
        PlotModel.InvalidatePlot(false);
    }

    [RelayCommand]
    private void ResetZoom()
    {
        PlotModel.ResetAllAxes();
        PlotModel.InvalidatePlot(false);
    }

    /// <summary>M4.1: 导出 CSV — 仅写当前可见 X 范围内的点。</summary>
    [RelayCommand]
    private void ExportCsv()
    {
        var dlg = new SaveFileDialog { Filter = "CSV 文件|*.csv", FileName = $"trend-{DateTime.Now:yyyyMMdd-HHmmss}.csv" };
        if (dlg.ShowDialog() != true) return;

        var xAxis = PlotModel.DefaultXAxis;
        double xMin = xAxis?.ActualMinimum ?? double.MinValue;
        double xMax = xAxis?.ActualMaximum ?? double.MaxValue;

        var sb = new StringBuilder();
        sb.AppendLine("Time,Series,Value");
        foreach (var s in PlotModel.Series.OfType<LineSeries>())
        {
            foreach (var p in s.Points)
            {
                if (p.X < xMin || p.X > xMax) continue;
                var t = DateTimeAxis.ToDateTime(p.X);
                sb.AppendLine($"{t:yyyy-MM-dd HH:mm:ss.fff},{Csv(s.Title)},{p.Y.ToString(CultureInfo.InvariantCulture)}");
            }
        }
        try { File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8); }
        catch { /* ignore */ }
    }

    [RelayCommand]
    private void ExportPng()
    {
        var dlg = new SaveFileDialog { Filter = "PNG 图片|*.png", FileName = $"trend-{DateTime.Now:yyyyMMdd-HHmmss}.png" };
        if (dlg.ShowDialog() != true) return;
        try
        {
            using var stream = File.Create(dlg.FileName);
            var exporter = new OxyPlot.Wpf.PngExporter { Width = 1200, Height = 600 };
            exporter.Export(PlotModel, stream);
        }
        catch { /* ignore */ }
    }

    private static string Csv(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        if (s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0)
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
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

    private static double ParseD(string s, double fallback)
        => double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : fallback;

    private class TraceConfig
    {
        public string TagId      { get; set; } = "";
        public string Color      { get; set; } = "#2563EB";
        public double LineWidth  { get; set; } = 1.5;
        public string Label      { get; set; } = "";
        public int    YAxisIndex { get; set; } = 1;
    }

    /// <summary>M4.1 Ruler popup 表的一行（曲线名 + 当前值 + 颜色色块）。</summary>
    public class RulerRow
    {
        public string Label { get; set; } = "";
        public string ValueText { get; set; } = "";
        public Brush ColorBrush { get; set; } = Brushes.Black;
    }
}
