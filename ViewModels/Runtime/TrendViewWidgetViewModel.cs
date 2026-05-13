#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using ApexHMI.Models.RuntimeUi;
using ApexHMI.Services.RuntimeUi;
using Microsoft.Win32;

namespace ApexHMI.ViewModels.Runtime;

/// <summary>
/// P5B 趋势视图（OxyPlot）：
/// <list type="bullet">
///   <item>实时模式：订阅 traces 中各 tagId，滚动时间窗（默认 60s）</item>
///   <item>历史模式：从 TrendHistoryService 拉数据（第一版占位）</item>
/// </list>
/// 设计时无 Shell：渲染静态正弦波 demo。
/// </summary>
public partial class TrendViewWidgetViewModel : WidgetViewModelBase
{
    private readonly List<TraceConfig> _traceConfigs = new();
    private readonly Dictionary<string, LineSeries> _seriesByTag = new(StringComparer.OrdinalIgnoreCase);

    [ObservableProperty]
    private bool _paused;

    public TrendViewWidgetViewModel(WidgetInstance model, IWidgetDataContext dataContext)
        : base(model, dataContext)
    {
        PlotModel = BuildPlotModel();
        ParseTraces();
        InitSeries();

        // 实时模式：订阅 tag 回调
        if (string.Equals(Mode, "realtime", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var cfg in _traceConfigs)
            {
                if (string.IsNullOrWhiteSpace(cfg.TagId)) continue;
                var captured = cfg;
                dataContext.RegisterValueCallback(captured.TagId, raw => OnTagPushed(captured, raw));
            }

            // 设计时（DesignMode 上下文）—— Shell 为 null 或为 DesignerEditorViewModel 时插入 demo
            if (IsDesignTime())
            {
                SeedDemoData();
            }
        }
        else
        {
            // P10H 历史模式：从 SQLite 读取过去 TimeWindow 秒的数据
            LoadHistoryFromSqlite();
        }
    }

    public PlotModel PlotModel { get; }

    public string TracesRaw   => Prop("traces", "[]");
    public string Mode        => Prop("mode", "realtime"); // realtime / history
    public double TimeWindow  => ParseD(Prop("timeWindow", "60"), 60);
    public string YMinRaw     => Prop("yMin", "auto");
    public string YMaxRaw     => Prop("yMax", "auto");
    public bool ShowLegend    => string.Equals(Prop("showLegend", "true"), "true", StringComparison.OrdinalIgnoreCase);
    public bool ShowGrid      => string.Equals(Prop("showGrid", "true"), "true", StringComparison.OrdinalIgnoreCase);
    public bool ShowToolbar   => string.Equals(Prop("showToolbar", "true"), "true", StringComparison.OrdinalIgnoreCase);
    public string BgColor     => Prop("backgroundColor", "#FFFFFF");

    public Visibility ToolbarVisibility => ShowToolbar ? Visibility.Visible : Visibility.Collapsed;

    private PlotModel BuildPlotModel()
    {
        var pm = new PlotModel
        {
            PlotAreaBorderColor = OxyColor.FromArgb(255, 148, 163, 184),
            Padding = new OxyThickness(4),
        };

        pm.Axes.Add(new DateTimeAxis
        {
            Position = AxisPosition.Bottom,
            StringFormat = "HH:mm:ss",
            IntervalType = DateTimeIntervalType.Auto,
            MajorGridlineStyle = LineStyle.Dot,
            MajorGridlineColor = OxyColor.FromArgb(255, 226, 232, 240),
        });

        var ya = new LinearAxis
        {
            Position = AxisPosition.Left,
            MajorGridlineStyle = LineStyle.Dot,
            MajorGridlineColor = OxyColor.FromArgb(255, 226, 232, 240),
        };
        if (double.TryParse(YMinRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var ymin)) ya.Minimum = ymin;
        if (double.TryParse(YMaxRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var ymax)) ya.Maximum = ymax;
        pm.Axes.Add(ya);

        if (ShowLegend)
        {
            pm.IsLegendVisible = true;
        }

        return pm;
    }

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
                    TagId    = el.TryGetProperty("tagId", out var t)  ? t.GetString() ?? "" : "",
                    Color    = el.TryGetProperty("color", out var co) ? co.GetString() ?? "#2563EB" : "#2563EB",
                    LineWidth= el.TryGetProperty("lineWidth", out var lw) && lw.ValueKind == JsonValueKind.Number ? lw.GetDouble() : 1.5,
                    Label    = el.TryGetProperty("label", out var lb) ? lb.GetString() ?? "" : "",
                };
                if (!string.IsNullOrEmpty(c.TagId)) _traceConfigs.Add(c);
            }
        }
        catch { /* ignore */ }
    }

    private void InitSeries()
    {
        foreach (var cfg in _traceConfigs)
        {
            var s = new LineSeries
            {
                Title = string.IsNullOrEmpty(cfg.Label) ? cfg.TagId : cfg.Label,
                Color = ParseColor(cfg.Color),
                StrokeThickness = cfg.LineWidth,
                MarkerType = MarkerType.None,
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

        // 滚动时间窗：丢弃 (Now - timeWindow) 之前的点
        var cutoff = DateTimeAxis.ToDouble(DateTime.Now.AddSeconds(-TimeWindow));
        while (s.Points.Count > 0 && s.Points[0].X < cutoff) s.Points.RemoveAt(0);

        // 限点（保险）
        const int MaxPoints = 4000;
        while (s.Points.Count > MaxPoints) s.Points.RemoveAt(0);

        if (Application.Current?.Dispatcher is { } d && !d.CheckAccess())
            d.BeginInvoke(new Action(() => PlotModel.InvalidatePlot(true)));
        else
            PlotModel.InvalidatePlot(true);
    }

    /// <summary>P10H：从 SQLite tag_history 加载过去 TimeWindow 秒的样本。</summary>
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
                {
                    series.Points.Add(new DataPoint(DateTimeAxis.ToDouble(p.Time), p.Value));
                }
            }
            PlotModel.InvalidatePlot(true);
            if (_seriesByTag.Count == 0)
            {
                var ann = new LineSeries { Title = "[历史模式：未配置 traces]", Color = OxyColors.Gray };
                PlotModel.Series.Add(ann);
            }
        }
        catch
        {
            var ann = new LineSeries { Title = "[历史模式：查询失败]", Color = OxyColors.Gray };
            PlotModel.Series.Add(ann);
        }
    }

    private void SeedDemoData()
    {
        // 静态正弦波示例（2 条曲线）让设计时可见
        var s1 = new LineSeries { Title = "demo-sin", Color = OxyColors.SteelBlue, StrokeThickness = 1.5 };
        var s2 = new LineSeries { Title = "demo-cos", Color = OxyColors.IndianRed, StrokeThickness = 1.5 };
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
        var name = shell.GetType().Name;
        // DesignerEditorViewModel / MainViewModel 名字判断；DesignModeWidgetDataContext 已被 Shell 为 null 覆盖
        return name.Contains("Designer", StringComparison.OrdinalIgnoreCase);
    }

    [RelayCommand]
    private void TogglePause() => Paused = !Paused;

    [RelayCommand]
    private void ResetZoom()
    {
        PlotModel.ResetAllAxes();
        PlotModel.InvalidatePlot(false);
    }

    [RelayCommand]
    private void ExportCsv()
    {
        var dlg = new SaveFileDialog { Filter = "CSV 文件|*.csv", FileName = $"trend-{DateTime.Now:yyyyMMdd-HHmmss}.csv" };
        if (dlg.ShowDialog() != true) return;
        var sb = new StringBuilder();
        sb.Append("Time");
        foreach (var s in PlotModel.Series.OfType<LineSeries>()) sb.Append(',').Append(s.Title);
        sb.AppendLine();

        // 简化：按各序列独立时间轴罗列
        foreach (var s in PlotModel.Series.OfType<LineSeries>())
        {
            foreach (var p in s.Points)
            {
                var t = DateTimeAxis.ToDateTime(p.X);
                sb.AppendLine($"{t:yyyy-MM-dd HH:mm:ss.fff},{s.Title},{p.Y.ToString(CultureInfo.InvariantCulture)}");
            }
        }
        File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
    }

    [RelayCommand]
    private void ExportPng()
    {
        var dlg = new SaveFileDialog { Filter = "PNG 图片|*.png", FileName = $"trend-{DateTime.Now:yyyyMMdd-HHmmss}.png" };
        if (dlg.ShowDialog() != true) return;
        try
        {
            using var stream = File.Create(dlg.FileName);
            var exporter = new OxyPlot.Wpf.PngExporter
            {
                Width = 1200,
                Height = 600,
            };
            exporter.Export(PlotModel, stream);
        }
        catch { /* 静默：导出失败不致命 */ }
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
        public string TagId { get; set; } = "";
        public string Color { get; set; } = "#2563EB";
        public double LineWidth { get; set; } = 1.5;
        public string Label { get; set; } = "";
    }
}
