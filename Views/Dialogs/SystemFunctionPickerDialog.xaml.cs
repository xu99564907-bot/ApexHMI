#nullable enable
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ApexHMI.Services.RuntimeUi;

namespace ApexHMI.Views.Dialogs;

/// <summary>P1: 选择系统函数 ID 的对话框。按 Category 分组展示。</summary>
public partial class SystemFunctionPickerDialog : Window
{
    public string? SelectedFunctionId { get; private set; }

    public SystemFunctionPickerDialog()
    {
        InitializeComponent();
        BuildTree();
    }

    private void BuildTree()
    {
        var grouped = SystemFunctionCatalog.All.GroupBy(f => f.Category);
        foreach (var g in grouped)
        {
            var node = new TreeViewItem
            {
                Header = g.Key,
                IsExpanded = true,
                FontWeight = FontWeights.SemiBold,
            };
            foreach (var f in g)
            {
                var sp = new StackPanel { Orientation = Orientation.Horizontal };
                sp.Children.Add(new TextBlock
                {
                    Text = f.DisplayName,
                    FontWeight = FontWeights.Normal,
                    MinWidth = 110,
                });
                sp.Children.Add(new TextBlock
                {
                    Text = "— " + f.Description,
                    Foreground = System.Windows.Media.Brushes.Gray,
                    FontWeight = FontWeights.Normal,
                    Margin = new Thickness(8, 0, 0, 0),
                    FontSize = 11,
                });
                var item = new TreeViewItem
                {
                    Header = sp,
                    Tag = f.Id,
                    FontWeight = FontWeights.Normal,
                };
                node.Items.Add(item);
            }
            FunctionTree.Items.Add(node);
        }
    }

    private void OnDoubleClick(object sender, MouseButtonEventArgs e)
    {
        TryAccept();
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        TryAccept();
    }

    private void TryAccept()
    {
        if (FunctionTree.SelectedItem is TreeViewItem ti && ti.Tag is string id && !string.IsNullOrEmpty(id))
        {
            SelectedFunctionId = id;
            DialogResult = true;
            Close();
        }
    }
}
