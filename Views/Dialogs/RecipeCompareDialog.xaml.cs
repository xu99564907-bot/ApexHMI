using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using ApexHMI.Models;

namespace ApexHMI.Views.Dialogs;

public partial class RecipeCompareDialog : Window
{
    public RecipeCompareDialog() => InitializeComponent();

    private void Compare_Click(object sender, RoutedEventArgs e)
    {
        if (ComboA.SelectedItem is not RecipeItem a || ComboB.SelectedItem is not RecipeItem b)
        {
            MessageBox.Show("请先选择 A 和 B 两个配方", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (ReferenceEquals(a, b))
        {
            MessageBox.Show("请选择两个不同的配方", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var rows = new List<DiffRow>();
        var allNames = a.Parameters.Select(p => p.Name)
            .Union(b.Parameters.Select(p => p.Name), StringComparer.OrdinalIgnoreCase);

        foreach (var name in allNames.OrderBy(n => n, StringComparer.Ordinal))
        {
            var pa = a.Parameters.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            var pb = b.Parameters.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            var va = pa?.Value ?? "(无)";
            var vb = pb?.Value ?? "(无)";
            if (string.Equals(va, vb, StringComparison.Ordinal)) continue;

            rows.Add(new DiffRow
            {
                Name = name,
                ValueA = va,
                ValueB = vb,
                Unit = pa?.Unit ?? pb?.Unit ?? string.Empty,
                Description = pa?.Description ?? pb?.Description ?? string.Empty
            });
        }

        DiffGrid.ItemsSource = rows;
    }

    public sealed class DiffRow
    {
        public string Name { get; set; } = string.Empty;
        public string ValueA { get; set; } = string.Empty;
        public string ValueB { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}
