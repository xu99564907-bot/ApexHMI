using System.Collections.Generic;
using System.Linq;
using System.Windows;
using ApexHMI.Models;

namespace ApexHMI.Views.Dialogs;

public partial class TagBrowserDialog : Window
{
    private readonly List<TagItem> _allTags;
    public string? SelectedTagName { get; private set; }

    public TagBrowserDialog(IEnumerable<TagItem> tags)
    {
        InitializeComponent();
        _allTags = tags.ToList();
        var categories = new List<string> { "(全部)" };
        categories.AddRange(_allTags.Select(t => t.Category ?? "")
            .Distinct()
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .OrderBy(c => c));
        CategoryCombo.ItemsSource = categories;
        CategoryCombo.SelectedIndex = 0;
        ApplyFilter();
    }

    private void Filter_Changed(object sender, System.Windows.RoutedEventArgs e) => ApplyFilter();

    private void ApplyFilter()
    {
        if (TagListView is null) return;
        var keyword = SearchBox?.Text?.Trim() ?? string.Empty;
        var category = CategoryCombo?.SelectedItem as string;

        var query = _allTags.AsEnumerable();
        if (!string.IsNullOrEmpty(category) && category != "(全部)")
            query = query.Where(t => string.Equals(t.Category, category, System.StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(keyword))
            query = query.Where(t =>
                (t.Name?.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0) ||
                (t.NodeId?.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0));
        TagListView.ItemsSource = query.OrderBy(t => t.Name).ToList();
    }

    private void Confirm_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (TagListView.SelectedItem is TagItem t)
        {
            SelectedTagName = t.Name;
            DialogResult = true;
            Close();
        }
    }

    private void Cancel_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
