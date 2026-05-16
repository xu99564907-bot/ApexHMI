using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using ApexHMI.Services;

namespace ApexHMI.Views.Dialogs;

public partial class PlcVariableOpFilterDialog : Window
{
    public sealed class OpEntry : INotifyPropertyChanged
    {
        public string Key { get; init; } = string.Empty;
        public string DisplayText { get; init; } = string.Empty;

        private bool _isSelected = true;
        public bool IsSelected
        {
            get => _isSelected;
            set { if (_isSelected != value) { _isSelected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected))); } }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public ObservableCollection<OpEntry> Entries { get; } = new();

    /// <summary>用户点确定后才有效；取消则为 null。</summary>
    public ISet<string>? SelectedOps { get; private set; }

    public PlcVariableOpFilterDialog(IReadOnlyList<(string Group, string? Op)> topGroups)
    {
        InitializeComponent();

        // 按 OP key 聚合（OP10/OP20/…/__shared__），统计每个 OP 下的顶层组数量
        var grouped = topGroups
            .GroupBy(g => g.Op ?? PlcVariableImportService.UncategorizedOpKey)
            .Select(g => new
            {
                Key = g.Key,
                Count = g.Count(),
                Label = g.Key == PlcVariableImportService.UncategorizedOpKey ? "未分类 / 共享变量" : g.Key
            })
            .OrderBy(x => x.Key == PlcVariableImportService.UncategorizedOpKey ? "ZZZ" : x.Key, StringComparer.Ordinal);

        foreach (var item in grouped)
        {
            Entries.Add(new OpEntry
            {
                Key = item.Key,
                DisplayText = $"{item.Label}    （{item.Count} 个变量组）"
            });
        }

        OpListItems.ItemsSource = Entries;
        SummaryText.Text = $"共 {Entries.Count} 类 / {topGroups.Count} 个组";
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var e2 in Entries) e2.IsSelected = true;
    }

    private void SelectNone_Click(object sender, RoutedEventArgs e)
    {
        foreach (var e2 in Entries) e2.IsSelected = false;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        SelectedOps = new HashSet<string>(Entries.Where(x => x.IsSelected).Select(x => x.Key), StringComparer.Ordinal);
        DialogResult = true;
    }
}
