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

    // M6.5: 历史模式 Lazy Load — 记录已加载的时间区间（每 tag 独立），节流 + 缓存
    private readonly Dictionary<string, List<(DateTime From, DateTime To)>> _loadedRanges
        = new(StringComparer.OrdinalIgnoreCase);
    private System.Windows.Threading.DispatcherTimer? _lazyLoadDebounce;
    private (DateTime From, DateTime To)? _pendingLoadRange;
    private bool _xAxisHookInstalled;
    private ApexHMI.Services.TrendHistoryService? _historyService;

    [ObservableProperty] private bool _paused;
    [ObservableProperty] private bool _rulerVisible;
    [ObservableProperty] private string _rulerTimeText = string.Empty;
    /// <summary>M7.5: 当前可视范围使用的 LOD 状态文本（"原始"/"已降采样 1 point / 15min"等）。</summary>
    [ObservableProperty] private string _lodStatusText = string.Empty;

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
            _historyService ??= new ApexHMI.Services.TrendHistoryService();
            var to = DateTime.Now;
            var from = to.AddSeconds(-TimeWindow);
            LoadHistoryRange(from, to);

            // M6.5: 安装 X 轴变化监听 — zoom/pan 到未加载区间时触发 lazy load
            EnsureXAxisHook();
        }
        catch { /* ignore */ }
    }

    /// <summary>
    /// M6.5: 加载指定时间区间到所有 LineSeries（去重 + 排序合并），并记录到 _loadedRanges。
    /// M7.5: 按可视范围长度选 bucket，自动 LOD 降采样，避免拖远卡顿。
    /// </summary>
    private void LoadHistoryRange(DateTime from, DateTime to)
    {
        if (_historyService is null) return;
        if (from >= to) return;

        // M7.5: 选 bucket — 短跨度原始；长跨度走 QueryAggregated
        var span = to - from;
        var bucketMs = PickBucketMs(span);
        // 切换 LOD bucket 时清空已加载缓存（不同分辨率不能混合到一个 series）
        if (bucketMs != _activeBucketMs)
        {
            _activeBucketMs = bucketMs;
            _loadedRanges.Clear();
            foreach (var s in _seriesByTag.Values) s.Points.Clear();
        }
        LodStatusText = BuildLodStatusText(bucketMs);

        foreach (var kv in _seriesByTag)
        {
            var tagId = kv.Key;
            var series = kv.Value;
            // 仅查询未覆盖部分
            var gaps = ComputeMissingRanges(tagId, from, to);
            foreach (var gap in gaps)
            {
                IReadOnlyList<ApexHMI.Services.TrendHistoryPoint> pts;
                try
                {
                    pts = bucketMs > 0
                        ? _historyService.QueryAggregated(tagId, gap.From, gap.To, bucketMs)
                        : _historyService.Query(tagId, gap.From, gap.To);
                }
                catch { continue; }
                if (pts.Count == 0)
                {
                    RecordLoadedRange(tagId, gap.From, gap.To);
                    continue;
                }
                MergePointsIntoSeries(series, pts);
                RecordLoadedRange(tagId, gap.From, gap.To);
            }
        }
        PlotModel.InvalidatePlot(true);
    }

    /// <summary>M7.5: 当前选用的 bucket 宽度（毫秒），0 = 原始无聚合。</summary>
    private long _activeBucketMs = -1;

    /// <summary>M7.5: 按可视跨度选 bucket。</summary>
    /// <list type="bullet">
    ///   <item>&lt; 1h → 原始</item>
    ///   <item>1h - 12h → 60s</item>
    ///   <item>12h - 7d → 900s</item>
    ///   <item>&gt; 7d → 3600s</item>
    /// </list>
    private static long PickBucketMs(TimeSpan span)
    {
        if (span < TimeSpan.FromHours(1)) return 0;
        if (span < TimeSpan.FromHours(12)) return 60_000;
        if (span < TimeSpan.FromDays(7)) return 900_000;
        return 3_600_000;
    }

    private static string BuildLodStatusText(long bucketMs) => bucketMs switch
    {
        <= 0      => "LOD：原始（无降采样）",
        60_000    => "已降采样 1 point / 1min",
        900_000   => "已降采样 1 point / 15min",
        3_600_000 => "已降采样 1 point / 1h",
        _ => $"已降采样 bucket={bucketMs}ms",
    };

    private static void MergePointsIntoSeries(LineSeries series, IReadOnlyList<ApexHMI.Services.TrendHistoryPoint> samples)
    {
        // OxyPlot LineSeries.Points 期望按 X 升序；这里把新点插入到正确位置（小集合简单 sort）
        foreach (var s in samples)
        {
            series.Points.Add(new DataPoint(DateTimeAxis.ToDouble(s.Time), s.Value));
        }
        series.Points.Sort((a, b) => a.X.CompareTo(b.X));
    }

    private List<(DateTime From, DateTime To)> ComputeMissingRanges(string tagId, DateTime from, DateTime to)
    {
        // 相交检测：把请求区间 [from, to] 减去已加载区间，返回剩余 gap 段
        if (!_loadedRanges.TryGetValue(tagId, out var loaded) || loaded.Count == 0)
            return new List<(DateTime, DateTime)> { (from, to) };

        var result = new List<(DateTime, DateTime)>();
        var cursor = from;
        var sorted = loaded.Where(r => r.To > from && r.From < to).OrderBy(r => r.From).ToList();
        foreach (var r in sorted)
        {
            if (r.From > cursor)
            {
                var gapEnd = r.From < to ? r.From : to;
                if (gapEnd > cursor) result.Add((cursor, gapEnd));
            }
            if (r.To > cursor) cursor = r.To;
            if (cursor >= to) break;
        }
        if (cursor < to) result.Add((cursor, to));
        return result;
    }

    private void RecordLoadedRange(string tagId, DateTime from, DateTime to)
    {
        if (!_loadedRanges.TryGetValue(tagId, out var list))
        {
            list = new List<(DateTime, DateTime)>();
            _loadedRanges[tagId] = list;
        }
        list.Add((from, to));
        // 合并相邻 / 相交区间，控制列表增长
        list.Sort((a, b) => a.From.CompareTo(b.From));
        var merged = new List<(DateTime From, DateTime To)>();
        foreach (var r in list)
        {
            if (merged.Count > 0 && r.From <= merged[^1].To)
            {
                var last = merged[^1];
                if (r.To > last.To) merged[^1] = (last.From, r.To);
            }
            else
            {
                merged.Add(r);
            }
        }
        _loadedRanges[tagId] = merged;
    }

    /// <summary>M6.5: 把 X 轴 AxisChanged 事件挂上 → 触发 200ms debounce 后的 lazy load。</summary>
    private void EnsureXAxisHook()
    {
        if (_xAxisHookInstalled) return;
        var x = PlotModel.Axes.OfType<DateTimeAxis>().FirstOrDefault();
        if (x is null) return;
        x.AxisChanged += OnXAxisChanged;
        _xAxisHookInstalled = true;
    }

    private void OnXAxisChanged(object? sender, AxisChangedEventArgs e)
    {
        if (sender is not DateTimeAxis ax) return;
        if (!string.Equals(Mode, "history", StringComparison.OrdinalIgnoreCase)) return;
        try
        {
            var from = DateTimeAxis.ToDateTime(ax.ActualMinimum);
            var to   = DateTimeAxis.ToDateTime(ax.ActualMaximum);
            if (to <= from) return;
            _pendingLoadRange = (from, to);
            // Debounce 200ms — 拖动 / 滚轮高频时只在停顿后查询一次
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher is null) return;
            if (_lazyLoadDebounce is null)
            {
                _lazyLoadDebounce = new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.Background, dispatcher)
                {
                    Interval = TimeSpan.FromMilliseconds(200),
                };
                _lazyLoadDebounce.Tick += LazyLoadDebounceTick;
            }
            _lazyLoadDebounce.Stop();
            _lazyLoadDebounce.Start();
        }
        catch { /* ignore */ }
    }

    private void LazyLoadDebounceTick(object? sender, EventArgs e)
    {
        _lazyLoadDebounce?.Stop();
        var range = _pendingLoadRange;
        _pendingLoadRange = null;
        if (range is null) return;
        try
        {
            LoadHistoryRange(range.Value.From, range.Value.To);
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
