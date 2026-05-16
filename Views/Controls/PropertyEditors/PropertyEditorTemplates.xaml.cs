#nullable enable
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using ApexHMI.ViewModels.Modules;

namespace ApexHMI.Views.Controls.PropertyEditors;

/// <summary>
/// P7.5: PropertyEditorTemplates.xaml 的代码后置，承载颜色编辑器 Popup 的事件处理。
/// </summary>
public partial class PropertyEditorTemplates : ResourceDictionary
{
    public PropertyEditorTemplates()
    {
        InitializeComponent();
    }

    /// <summary>常用色块点击 → 写回 VM.Value，并关闭 Popup。</summary>
    private void ColorSwatch_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement fe) return;
        if (fe.Tag is not string color) return;
        ApplyValue(fe, color);
    }

    /// <summary>样式引用按钮点击。</summary>
    private void ColorStyleRef_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        if (btn.Tag is not string value) return;
        ApplyValue(btn, value);
    }

    /// <summary>自定义颜色 "应用" 按钮。</summary>
    private void ColorCustomApply_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        // 同一 Popup 内的 CustomColorBox
        var popup = FindAncestor<Popup>(btn);
        if (popup is null) return;
        var box = FindDescendant<TextBox>(popup.Child as FrameworkElement, "CustomColorBox");
        if (box is null) return;
        ApplyValue(btn, box.Text);
    }

    private static void ApplyValue(FrameworkElement source, string newValue)
    {
        if (source.DataContext is PropertyEditorVM vm)
        {
            vm.Value = newValue;
        }
        // 关闭 Popup
        var popup = FindAncestor<Popup>(source);
        if (popup is not null) popup.IsOpen = false;
    }

    private static T? FindAncestor<T>(DependencyObject? d) where T : DependencyObject
    {
        while (d is not null)
        {
            if (d is T t) return t;
            d = System.Windows.Media.VisualTreeHelper.GetParent(d)
                ?? LogicalTreeHelperParent(d);
        }
        return null;
    }

    private static DependencyObject? LogicalTreeHelperParent(DependencyObject d)
        => System.Windows.LogicalTreeHelper.GetParent(d);

    private static T? FindDescendant<T>(FrameworkElement? root, string name) where T : FrameworkElement
    {
        if (root is null) return null;
        if (root is T tr && tr.Name == name) return tr;
        var count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i) as FrameworkElement;
            var found = FindDescendant<T>(child, name);
            if (found is not null) return found;
        }
        // 也找 logical children（Popup 子树可能不在 visual tree 起始）
        if (root is Panel p)
        {
            foreach (var c in p.Children)
                if (c is FrameworkElement fe)
                {
                    var found = FindDescendant<T>(fe, name);
                    if (found is not null) return found;
                }
        }
        return null;
    }
}
