#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using ApexHMI.Models.RuntimeUi;

namespace ApexHMI.Services.RuntimeUi;

/// <summary>
/// P2 动画运行时引擎：根据 <see cref="WidgetInstance.Appearance"/> / <see cref="WidgetInstance.Visibility"/>
/// / <see cref="WidgetInstance.Movement"/> 三类新动画模型，订阅 PLC 值变化驱动 view 视觉。
/// <para>独立于 widget VM；外部在 view 创建完毕后调一次 <see cref="Subscribe"/>。</para>
/// <para>闪烁效果由全局 600ms DispatcherTimer 周期切换 Opacity；多个动画的闪烁状态独立维护。</para>
/// </summary>
public static class AnimationEngine
{
    // 当前注册了"闪烁"的 view 集合（弱引用避免阻碍 GC）。
    private static readonly List<WeakReference<FrameworkElement>> _blinking = new();
    private static bool _blinkPhaseOn = true;
    private static readonly DispatcherTimer _blinkTimer;

    static AnimationEngine()
    {
        _blinkTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(600),
        };
        _blinkTimer.Tick += (_, _) => TickBlink();
        _blinkTimer.Start();
    }

    private static void TickBlink()
    {
        _blinkPhaseOn = !_blinkPhaseOn;
        // 清理已释放的弱引用
        for (int i = _blinking.Count - 1; i >= 0; i--)
        {
            if (!_blinking[i].TryGetTarget(out var view))
            {
                _blinking.RemoveAt(i);
                continue;
            }
            view.Opacity = _blinkPhaseOn ? 1.0 : 0.3;
        }
    }

    private static void SetBlink(FrameworkElement view, bool blink)
    {
        // 已存在 → 决定保留或删除
        for (int i = _blinking.Count - 1; i >= 0; i--)
        {
            if (!_blinking[i].TryGetTarget(out var existing))
            {
                _blinking.RemoveAt(i);
                continue;
            }
            if (ReferenceEquals(existing, view))
            {
                if (!blink)
                {
                    _blinking.RemoveAt(i);
                    view.Opacity = 1.0;
                }
                return;
            }
        }
        if (blink) _blinking.Add(new WeakReference<FrameworkElement>(view));
        else view.Opacity = 1.0;
    }

    /// <summary>给 widget view 挂载新动画订阅。多次调用安全（每次重新订阅）。</summary>
    public static void Subscribe(WidgetInstance widget, FrameworkElement view, IWidgetDataContext ctx)
    {
        if (widget is null || view is null || ctx is null) return;

        // -------- Appearance --------
        if (widget.Appearance is { } app && !string.IsNullOrWhiteSpace(app.TagId))
        {
            var captured = app;
            // 设计时静态预览：默认显示第一行的颜色（如果有）
            ApplyAppearanceRow(view, captured.Rows.Count > 0 ? captured.Rows[0] : null);
            ctx.RegisterValueCallback(captured.TagId, val =>
            {
                var row = MatchAppearanceRow(captured, val);
                ApplyAppearanceRow(view, row);
            });
        }

        // -------- Visibility --------
        if (widget.Visibility is { } vis && !string.IsNullOrWhiteSpace(vis.TagId))
        {
            var captured = vis;
            ctx.RegisterValueCallback(captured.TagId, val => ApplyVisibility(view, captured, val));
        }

        // -------- Movement --------
        if (widget.Movement is { } mv)
        {
            var captured = mv;
            // 用 TranslateTransform 实现位移，不破坏 Canvas.Left/Top 原始定位
            if (view.RenderTransform is not TranslateTransform)
                view.RenderTransform = new TranslateTransform(0, 0);

            void OnX(string val) => UpdateMoveAxis(view, captured, axisX: true, raw: val);
            void OnY(string val) => UpdateMoveAxis(view, captured, axisX: false, raw: val);

            switch (captured.MoveType)
            {
                case MoveType.Horizontal:
                    if (!string.IsNullOrWhiteSpace(captured.TagIdX)) ctx.RegisterValueCallback(captured.TagIdX, OnX);
                    break;
                case MoveType.Vertical:
                    if (!string.IsNullOrWhiteSpace(captured.TagIdY)) ctx.RegisterValueCallback(captured.TagIdY, OnY);
                    break;
                case MoveType.Direct:
                case MoveType.Diagonal:
                    if (!string.IsNullOrWhiteSpace(captured.TagIdX)) ctx.RegisterValueCallback(captured.TagIdX, OnX);
                    if (!string.IsNullOrWhiteSpace(captured.TagIdY)) ctx.RegisterValueCallback(captured.TagIdY, OnY);
                    break;
            }
        }
    }

    // =====================================================================
    // Appearance
    // =====================================================================

    private static AppearanceRow? MatchAppearanceRow(AppearanceAnimation app, string raw)
    {
        if (!double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var num))
        {
            // 非数字：True/False 当作 1/0
            num = raw is "True" or "true" or "TRUE" or "1" ? 1 : 0;
        }
        long ival = (long)num;
        foreach (var row in app.Rows)
        {
            switch (app.MatchType)
            {
                case AppearanceMatchType.Range:
                    if (double.TryParse(row.RangeFrom, NumberStyles.Any, CultureInfo.InvariantCulture, out var from) &&
                        double.TryParse(row.RangeTo,   NumberStyles.Any, CultureInfo.InvariantCulture, out var to))
                    {
                        var lo = Math.Min(from, to);
                        var hi = Math.Max(from, to);
                        if (num >= lo && num <= hi) return row;
                    }
                    break;
                case AppearanceMatchType.SingleBit:
                    if (((ival >> row.BitIndex) & 1L) == 1L) return row;
                    break;
                case AppearanceMatchType.MultiBit:
                    if (TryParseMask(row.BitMask, out var mask) && (ival & mask) != 0)
                        return row;
                    break;
            }
        }
        return null;
    }

    private static bool TryParseMask(string s, out long mask)
    {
        mask = 0;
        if (string.IsNullOrWhiteSpace(s)) return false;
        s = s.Trim();
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return long.TryParse(s.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out mask);
        return long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out mask);
    }

    private static void ApplyAppearanceRow(FrameworkElement view, AppearanceRow? row)
    {
        var dispatcher = view.Dispatcher;
        if (!dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(new Action(() => ApplyAppearanceRow(view, row)));
            return;
        }
        if (row is null)
        {
            SetBlink(view, false);
            return;
        }

        var bg = ParseBrush(row.Background);
        var fg = ParseBrush(row.Foreground);

        // 找 view 内的 Control / Panel，应用 Background / Foreground
        var target = FindStylableTarget(view);
        if (target is Control ctrl)
        {
            if (bg is not null) ctrl.Background = bg;
            if (fg is not null) ctrl.Foreground = fg;
        }
        else if (target is Panel panel)
        {
            if (bg is not null) panel.Background = bg;
        }
        else if (target is Border border)
        {
            if (bg is not null) border.Background = bg;
        }

        SetBlink(view, row.Blink);
    }

    private static FrameworkElement FindStylableTarget(FrameworkElement view)
    {
        // 优先找 view 下第一个 Control（如 Button）
        if (VisualTreeHelper.GetChildrenCount(view) > 0)
        {
            var child = VisualTreeHelper.GetChild(view, 0);
            if (child is Control c) return c;
            if (child is Panel p) return p;
            if (child is Border b) return b;
        }
        return view;
    }

    private static Brush? ParseBrush(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        try
        {
            var conv = new BrushConverter();
            return conv.ConvertFromString(s) as Brush;
        }
        catch
        {
            return null;
        }
    }

    // =====================================================================
    // Visibility
    // =====================================================================

    private static void ApplyVisibility(FrameworkElement view, VisibilityAnimation vis, string raw)
    {
        var dispatcher = view.Dispatcher;
        if (!dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(new Action(() => ApplyVisibility(view, vis, raw)));
            return;
        }

        bool show;
        switch (vis.Mode)
        {
            case VisibilityMode.WhenTrue:
                show = raw is "1" or "True" or "true" or "TRUE";
                break;
            case VisibilityMode.WhenFalse:
                show = !(raw is "1" or "True" or "true" or "TRUE");
                break;
            case VisibilityMode.WhenInRange:
                if (double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var num) &&
                    double.TryParse(vis.RangeFrom, NumberStyles.Any, CultureInfo.InvariantCulture, out var lo) &&
                    double.TryParse(vis.RangeTo,   NumberStyles.Any, CultureInfo.InvariantCulture, out var hi))
                {
                    var a = Math.Min(lo, hi); var b = Math.Max(lo, hi);
                    show = num >= a && num <= b;
                }
                else show = false;
                break;
            default: show = true; break;
        }

        if (show)
        {
            view.Visibility = System.Windows.Visibility.Visible;
            view.IsEnabled = true;
        }
        else if (vis.Otherwise == VisibilityOtherwise.Disabled)
        {
            view.Visibility = System.Windows.Visibility.Visible;
            view.IsEnabled = false;
        }
        else
        {
            view.Visibility = System.Windows.Visibility.Collapsed;
        }
    }

    // =====================================================================
    // Movement
    // =====================================================================

    private static void UpdateMoveAxis(FrameworkElement view, MoveAnimation mv, bool axisX, string raw)
    {
        var dispatcher = view.Dispatcher;
        if (!dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(new Action(() => UpdateMoveAxis(view, mv, axisX, raw)));
            return;
        }
        if (view.RenderTransform is not TranslateTransform tt)
        {
            tt = new TranslateTransform(0, 0);
            view.RenderTransform = tt;
        }
        if (!double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var v)) return;

        double pixel;
        if (mv.MoveType == MoveType.Direct)
        {
            // 直接：变量值即像素值（相对 0 起点）
            pixel = v;
        }
        else
        {
            // 线性插值
            if (axisX)
                pixel = MapLinear(v, mv.RangeMinX, mv.RangeMaxX, mv.PixelStartX, mv.PixelEndX);
            else
                pixel = MapLinear(v, mv.RangeMinY, mv.RangeMaxY, mv.PixelStartY, mv.PixelEndY);
        }

        if (axisX) tt.X = pixel;
        else       tt.Y = pixel;
    }

    private static double MapLinear(double v, double inMin, double inMax, double outMin, double outMax)
    {
        if (Math.Abs(inMax - inMin) < 1e-9) return outMin;
        var t = (v - inMin) / (inMax - inMin);
        if (t < 0) t = 0; else if (t > 1) t = 1;
        return outMin + t * (outMax - outMin);
    }
}
