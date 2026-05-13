using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using ApexHMI.ViewModels.Runtime;

namespace ApexHMI.Views.Runtime.Widgets;

public partial class TableViewWidget : UserControl
{
    public TableViewWidget()
    {
        InitializeComponent();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is not TableViewWidgetViewModel vm) return;

        BuildColumns(vm);
        Grid.HeadersVisibility = vm.ShowHeader ? DataGridHeadersVisibility.Column : DataGridHeadersVisibility.None;
        Grid.IsReadOnly = !vm.AllowEdit;

        // 列变更时重建（兼容动态属性修改）
        if (vm.Columns is INotifyCollectionChanged ncc)
        {
            ncc.CollectionChanged += (_, __) => BuildColumns(vm);
        }
    }

    private void BuildColumns(TableViewWidgetViewModel vm)
    {
        Grid.Columns.Clear();
        foreach (var spec in vm.Columns)
        {
            var col = new DataGridTextColumn
            {
                Header = string.IsNullOrEmpty(spec.Title) ? spec.Key : spec.Title,
                Width = new DataGridLength(spec.Width),
                Binding = new Binding($"Cells[{spec.Key}]") { Mode = vm.AllowEdit ? BindingMode.TwoWay : BindingMode.OneWay }
            };
            Grid.Columns.Add(col);
        }
    }
}
