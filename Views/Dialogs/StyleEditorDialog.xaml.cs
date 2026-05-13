#nullable enable
using System.Windows;
using ApexHMI.Models.RuntimeUi;
using ApexHMI.Services.RuntimeUi;

namespace ApexHMI.Views.Dialogs;

/// <summary>P6A: 全局样式编辑器（色板 + 字体预设）。</summary>
public partial class StyleEditorDialog : Window
{
    private readonly StyleDefinitions _styles;

    public StyleEditorDialog(StyleDefinitions styles)
    {
        InitializeComponent();
        _styles = styles;
        ColorGrid.ItemsSource = _styles.Colors;
        FontGrid.ItemsSource = _styles.Fonts;
    }

    private void OnAddColor(object sender, RoutedEventArgs e)
    {
        _styles.Colors.Add(new ColorPalette { Key = "new", Name = "新色", Value = "#888888" });
    }

    private void OnRemoveColor(object sender, RoutedEventArgs e)
    {
        if (ColorGrid.SelectedItem is ColorPalette c) _styles.Colors.Remove(c);
    }

    private void OnAddFont(object sender, RoutedEventArgs e)
    {
        _styles.Fonts.Add(new FontPreset { Key = "new", Name = "新字体" });
    }

    private void OnRemoveFont(object sender, RoutedEventArgs e)
    {
        if (FontGrid.SelectedItem is FontPreset f) _styles.Fonts.Remove(f);
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        // 通知所有 widget VM 刷新（颜色/字体改动 → UI 联动）
        DesignerContext.NotifyResourcesChanged();
        DialogResult = true;
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
