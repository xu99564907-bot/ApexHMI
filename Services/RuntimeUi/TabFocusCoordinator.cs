#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ApexHMI.Models.RuntimeUi;
using ApexHMI.ViewModels.Runtime;

namespace ApexHMI.Services.RuntimeUi;

/// <summary>
/// M3.4: 运行时画面 Tab 焦点流转协调器。
/// WinCC 真实行为：画面所有 I/O 域按 widget.Properties["tabIndex"] 升序形成全局 Tab 链；
/// Tab 移到下一个、Shift+Tab 移到上一个、循环。
/// 调用方法：DynamicPageHost.PreviewKeyDown += TabFocusCoordinator.HandlePreviewKeyDown;
/// </summary>
public static class TabFocusCoordinator
{
    /// <summary>判定一个 widget VM 是否参与 Tab 链（可输入 widget）。</summary>
    private static bool IsTabbable(WidgetViewModelBase? vm) => vm switch
    {
        IoNumericWidgetViewModel n => n.IsInput,
        IoSymbolicWidgetViewModel s => s.IsInput,
        DateTimeWidgetViewModel d => d.IsInput,
        // io-graphic / checkbox / switch / button 通过 keyboard nav (focusable + IsTabStop) 默认支持，
        // 此处只协调"按 tabIndex 跨 widget 流转"。
        _ => false,
    };

    public static void HandlePreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Tab) return;
        if (sender is not FrameworkElement root) return;

        var shift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
        var chain = BuildTabChain(root);
        if (chain.Count == 0) return;

        // 找到当前 focus 所在的链节点
        var focused = Keyboard.FocusedElement as DependencyObject;
        int currentIndex = -1;
        for (int i = 0; i < chain.Count; i++)
        {
            if (focused is null) break;
            if (IsAncestor(chain[i].Element, focused))
            {
                currentIndex = i;
                break;
            }
        }

        int nextIndex = shift
            ? (currentIndex <= 0 ? chain.Count - 1 : currentIndex - 1)
            : (currentIndex < 0 || currentIndex >= chain.Count - 1 ? 0 : currentIndex + 1);

        var target = chain[nextIndex].Element;
        // 优先把焦点给内部 TextBox（io-numeric 的实际输入元素）
        var inner = FindFirstFocusableDescendant(target);
        (inner ?? target).Focus();
        Keyboard.Focus(inner ?? target);
        e.Handled = true;
    }

    private struct TabEntry
    {
        public int TabIndex;
        public FrameworkElement Element;
    }

    private static List<TabEntry> BuildTabChain(DependencyObject root)
    {
        var list = new List<TabEntry>();
        Walk(root, list);
        return list
            .OrderBy(t => t.TabIndex)
            .ToList();
    }

    private static void Walk(DependencyObject node, List<TabEntry> list)
    {
        if (node is FrameworkElement fe && fe.DataContext is WidgetViewModelBase vm && IsTabbable(vm))
        {
            int idx = int.MaxValue;
            if (vm.Model.Properties.TryGetValue("tabIndex", out var raw)
                && int.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
            {
                idx = parsed;
            }
            list.Add(new TabEntry { TabIndex = idx, Element = fe });
            // 不向下钻：每个 widget 节点只算一次
            return;
        }

        var count = VisualTreeHelper.GetChildrenCount(node);
        for (int i = 0; i < count; i++)
        {
            Walk(VisualTreeHelper.GetChild(node, i), list);
        }
    }

    private static bool IsAncestor(DependencyObject ancestor, DependencyObject descendant)
    {
        var cur = descendant;
        while (cur is not null)
        {
            if (ReferenceEquals(cur, ancestor)) return true;
            cur = VisualTreeHelper.GetParent(cur) ?? (cur is FrameworkElement fe ? fe.Parent : null);
        }
        return false;
    }

    private static FrameworkElement? FindFirstFocusableDescendant(DependencyObject node)
    {
        if (node is TextBox or ComboBox or DatePicker or CheckBox)
            return node as FrameworkElement;
        var count = VisualTreeHelper.GetChildrenCount(node);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(node, i);
            var hit = FindFirstFocusableDescendant(child);
            if (hit is not null) return hit;
        }
        return null;
    }
}
