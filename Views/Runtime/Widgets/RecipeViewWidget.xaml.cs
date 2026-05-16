#nullable enable
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Data;
using ApexHMI.Models.RuntimeUi;
using ApexHMI.ViewModels.Runtime;

namespace ApexHMI.Views.Runtime.Widgets;

/// <summary>P8A 配方视图。DataGrid 列在 DataContext / CurrentRecipe / Datasets 变化时动态重建。</summary>
public partial class RecipeViewWidget : UserControl
{
    private RecipeViewWidgetViewModel? _vm;

    public RecipeViewWidget()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += (_, _) => RebuildColumns();
    }

    private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (_vm is not null)
        {
            _vm.PropertyChanged -= OnVmPropertyChanged;
            if (_vm.CurrentRecipe is { } oldRecipe)
            {
                oldRecipe.Datasets.CollectionChanged -= OnDatasetsChanged;
            }
        }
        _vm = DataContext as RecipeViewWidgetViewModel;
        if (_vm is not null)
        {
            _vm.PropertyChanged += OnVmPropertyChanged;
            HookDatasets();
        }
        RebuildColumns();
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RecipeViewWidgetViewModel.CurrentRecipe)
            || e.PropertyName == nameof(RecipeViewWidgetViewModel.Datasets)
            || string.IsNullOrEmpty(e.PropertyName))
        {
            HookDatasets();
            RebuildColumns();
        }
    }

    private void HookDatasets()
    {
        if (_vm?.CurrentRecipe is { } r)
        {
            r.Datasets.CollectionChanged -= OnDatasetsChanged;
            r.Datasets.CollectionChanged += OnDatasetsChanged;
        }
    }

    private void OnDatasetsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        => RebuildColumns();

    private void RebuildColumns()
    {
        if (Grid is null) return;
        Grid.Columns.Clear();

        Grid.Columns.Add(new DataGridTextColumn
        {
            Header = "字段",
            Binding = new Binding(nameof(RecipeFieldRow.DisplayName)) { Mode = BindingMode.OneWay },
            Width = new DataGridLength(120),
            IsReadOnly = true,
        });
        Grid.Columns.Add(new DataGridTextColumn
        {
            Header = "类型",
            Binding = new Binding(nameof(RecipeFieldRow.TypeName)) { Mode = BindingMode.OneWay },
            Width = new DataGridLength(70),
            IsReadOnly = true,
        });
        Grid.Columns.Add(new DataGridTextColumn
        {
            Header = "单位",
            Binding = new Binding(nameof(RecipeFieldRow.Unit)) { Mode = BindingMode.OneWay },
            Width = new DataGridLength(60),
            IsReadOnly = true,
        });
        Grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Tag",
            Binding = new Binding(nameof(RecipeFieldRow.TagAddress)) { Mode = BindingMode.OneWay },
            Width = new DataGridLength(140),
            IsReadOnly = true,
        });

        var recipe = _vm?.CurrentRecipe;
        if (recipe is null) return;
        var allowEdit = _vm?.AllowEditDataset ?? true;

        // 摘除并重挂 CellEditEnding，确保只有一个订阅
        Grid.CellEditEnding -= OnCellEditEnding;
        if (allowEdit) Grid.CellEditEnding += OnCellEditEnding;

        for (int i = 0; i < recipe.Datasets.Count; i++)
        {
            var ds = recipe.Datasets[i];
            var col = new DataGridTextColumn
            {
                Header = ds.Name,
                Width = new DataGridLength(120),
                IsReadOnly = !allowEdit,
                // OneWay 绑定通过 Converter 显示；编辑由 CellEditEnding 处理
                Binding = new Binding
                {
                    Mode = BindingMode.OneWay,
                    Converter = new RecipeValueConverter(ds),
                },
            };
            // 把数据集 Id 挂在列上，便于 CellEditEnding 取到
            col.SetValue(System.Windows.FrameworkElement.TagProperty, ds);
            Grid.Columns.Add(col);
        }
    }

    private void OnCellEditEnding(object? sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.EditAction != DataGridEditAction.Commit) return;
        if (e.Row?.Item is not RecipeFieldRow row) return;
        if (e.Column?.GetValue(System.Windows.FrameworkElement.TagProperty) is not RecipeDataset ds) return;
        if (e.EditingElement is TextBox tb)
        {
            row.SetValue(ds, tb.Text ?? string.Empty);
            // 触发列刷新（重新跑 Converter.Convert）
            Grid.Items.Refresh();
        }
    }
}

/// <summary>取 RecipeFieldRow 在某一数据集中的字符串值（只读显示）。</summary>
internal sealed class RecipeValueConverter : IValueConverter
{
    private readonly RecipeDataset _ds;
    public RecipeValueConverter(RecipeDataset ds) { _ds = ds; }

    public object? Convert(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
        => value is RecipeFieldRow row ? row.GetValue(_ds) : string.Empty;

    public object? ConvertBack(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;
}
