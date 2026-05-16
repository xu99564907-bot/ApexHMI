using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using ApexHMI.Models;
using ApexHMI.ViewModels.Modules;

namespace ApexHMI.Behaviors;

/// <summary>
/// 可附加到任意 TextBox 上的 Tag 自动补全行为：
/// <list type="bullet">
///   <item>用户键盘聚焦该 TextBox 输入时，弹出下一级候选（按 '.' / '[]' 分段，与 InoProShop 风格一致）</item>
///   <item>↑/↓ 切换、Enter/Tab 补全、Esc 关闭、鼠标点选</item>
///   <item>非键盘聚焦的文本变化（VM 写回 / 切换控件）不弹</item>
/// </list>
/// 候选源：向上查找最近的 <see cref="DesignerEditorViewModel"/>.AvailableTags。
/// 用法：<c>&lt;TextBox b:TagAutoComplete.IsEnabled="True" .../&gt;</c>
/// </summary>
public static class TagAutoComplete
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached("IsEnabled", typeof(bool), typeof(TagAutoComplete),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static void SetIsEnabled(DependencyObject d, bool v) => d.SetValue(IsEnabledProperty, v);
    public static bool GetIsEnabled(DependencyObject d) => (bool)d.GetValue(IsEnabledProperty);

    private static readonly DependencyProperty StateProperty =
        DependencyProperty.RegisterAttached("State", typeof(State), typeof(TagAutoComplete));

    private sealed class State
    {
        public Popup Popup = null!;
        public ListBox List = null!;
        public bool Suppress;
    }

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox tb) return;
        if (Equals(e.NewValue, true)) Attach(tb);
    }

    private static void Attach(TextBox tb)
    {
        var list = new ListBox
        {
            BorderThickness = new Thickness(0),
            FontSize = 12,
            MaxHeight = 240,
            FontFamily = new FontFamily("Consolas")
        };
        var border = new Border
        {
            Background = Brushes.White,
            BorderBrush = (Brush)new BrushConverter().ConvertFromString("#CBD5E1")!,
            BorderThickness = new Thickness(1),
            MinWidth = 220,
            MaxHeight = 240,
            Child = list
        };
        var popup = new Popup
        {
            PlacementTarget = tb,
            Placement = PlacementMode.Bottom,
            StaysOpen = false,
            AllowsTransparency = true,
            Child = border
        };
        var s = new State { Popup = popup, List = list };
        tb.SetValue(StateProperty, s);
        tb.TextChanged += (_, _) => OnTextChanged(tb);
        tb.PreviewKeyDown += (_, ke) => OnPreviewKeyDown(tb, ke);
        tb.LostKeyboardFocus += (_, _) => popup.IsOpen = false;
        list.PreviewMouseLeftButtonUp += (_, me) => OnListClick(tb, me);
    }

    private static State? GetState(TextBox tb) => tb.GetValue(StateProperty) as State;

    private static void OnTextChanged(TextBox tb)
    {
        var s = GetState(tb);
        if (s is null || s.Suppress) return;
        if (!tb.IsKeyboardFocusWithin) return;
        UpdateSuggestions(tb, s);
    }

    private static void OnPreviewKeyDown(TextBox tb, KeyEventArgs e)
    {
        var s = GetState(tb);
        if (s is null || !s.Popup.IsOpen) return;
        if (s.List.Items.Count == 0) return;
        switch (e.Key)
        {
            case Key.Down:
                s.List.SelectedIndex = Math.Min(s.List.SelectedIndex + 1, s.List.Items.Count - 1);
                s.List.ScrollIntoView(s.List.SelectedItem);
                e.Handled = true; break;
            case Key.Up:
                s.List.SelectedIndex = Math.Max(s.List.SelectedIndex - 1, 0);
                s.List.ScrollIntoView(s.List.SelectedItem);
                e.Handled = true; break;
            case Key.Tab:
            case Key.Enter:
                if (s.List.SelectedIndex < 0) s.List.SelectedIndex = 0;
                ApplySuggestion(tb, s, s.List.SelectedItem as string);
                e.Handled = true; break;
            case Key.Escape:
                s.Popup.IsOpen = false;
                e.Handled = true; break;
        }
    }

    private static void OnListClick(TextBox tb, MouseButtonEventArgs e)
    {
        var s = GetState(tb);
        if (s is null) return;
        var item = ItemsControl.ContainerFromElement(s.List, e.OriginalSource as DependencyObject) as ListBoxItem;
        if (item is null) return;
        ApplySuggestion(tb, s, item.Content as string);
    }

    private static (string prefix, string partial) SplitAtCaret(string text, int caret)
    {
        if (caret > text.Length) caret = text.Length;
        var head = text.Substring(0, caret);
        int boundary = -1;
        for (int i = head.Length - 1; i >= 0; i--)
        {
            var c = head[i];
            if (c == '.' || c == '[' || c == ']') { boundary = i; break; }
        }
        if (boundary < 0) return (string.Empty, head);
        return (head.Substring(0, boundary + 1), head.Substring(boundary + 1));
    }

    private static IEnumerable<string> NextSegments(IEnumerable<TagItem> tags, string prefix, string partial)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var tag in tags)
        {
            var name = tag.Name;
            if (string.IsNullOrEmpty(name)) continue;
            if (!name.StartsWith(prefix, StringComparison.Ordinal)) continue;
            var rest = name.Substring(prefix.Length);
            if (rest.Length == 0) continue;
            int next = rest.IndexOfAny(new[] { '.', '[' });
            var seg = next < 0 ? rest : rest.Substring(0, next);
            if (seg.Length == 0 && rest[0] == '[')
            {
                int close = rest.IndexOf(']');
                if (close > 0) seg = rest.Substring(0, close + 1);
            }
            if (string.IsNullOrEmpty(seg)) continue;
            if (!string.IsNullOrEmpty(partial) &&
                !seg.StartsWith(partial, StringComparison.OrdinalIgnoreCase)) continue;
            if (seen.Add(seg)) yield return seg;
        }
    }

    private static IEnumerable<TagItem>? FindTagsProvider(DependencyObject d)
    {
        DependencyObject? node = d;
        while (node != null)
        {
            if (node is FrameworkElement fe && fe.DataContext is DesignerEditorViewModel vm)
                return vm.AvailableTags;
            node = VisualTreeHelper.GetParent(node) ?? LogicalTreeHelper.GetParent(node);
        }
        return null;
    }

    private static void UpdateSuggestions(TextBox tb, State s)
    {
        var tags = FindTagsProvider(tb);
        if (tags is null) { s.Popup.IsOpen = false; return; }

        var text = tb.Text ?? string.Empty;
        var caret = tb.CaretIndex;
        var (prefix, partial) = SplitAtCaret(text, caret);

        var items = NextSegments(tags, prefix, partial)
            .OrderBy(x => x, StringComparer.Ordinal)
            .Take(200)
            .ToList();

        if (items.Count == 0 || (items.Count == 1 && string.Equals(items[0], partial, StringComparison.Ordinal)))
        {
            s.Popup.IsOpen = false;
            return;
        }

        s.List.ItemsSource = items;
        s.List.SelectedIndex = 0;
        s.Popup.IsOpen = true;
    }

    private static void ApplySuggestion(TextBox tb, State s, string? seg)
    {
        if (string.IsNullOrEmpty(seg)) return;
        var text = tb.Text ?? string.Empty;
        var caret = tb.CaretIndex;
        var (prefix, _) = SplitAtCaret(text, caret);
        var tail = text.Substring(caret);
        s.Suppress = true;
        try
        {
            tb.Text = prefix + seg + tail;
            tb.CaretIndex = (prefix + seg).Length;
        }
        finally { s.Suppress = false; }
        s.Popup.IsOpen = false;
        UpdateSuggestions(tb, s);
    }
}
