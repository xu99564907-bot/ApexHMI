using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using ApexHMI.Models;

namespace ApexHMI.Views.Dialogs;

public partial class TagBrowserDialog : Window
{
    private readonly List<TagItem> _allTags;
    public string? SelectedTagName { get; private set; }

    public ObservableCollection<TagTreeNode> Roots { get; } = new();

    public TagBrowserDialog(IEnumerable<TagItem> tags)
    {
        InitializeComponent();
        _allTags = tags.ToList();

        var categories = new List<string> { "(全部)" };
        categories.AddRange(_allTags.Select(t => t.Group ?? "")
            .Distinct()
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .OrderBy(c => c, StringComparer.Ordinal));
        CategoryCombo.ItemsSource = categories;
        CategoryCombo.SelectedIndex = 0;

        TagTreeView.ItemsSource = Roots;
        ApplyFilter();
    }

    private void Filter_Changed(object sender, RoutedEventArgs e) => ApplyFilter();
    private void Filter_Changed(object sender, TextChangedEventArgs e) => ApplyFilter();

    private void ApplyFilter()
    {
        if (TagTreeView is null) return;
        var keyword = SearchBox?.Text?.Trim() ?? string.Empty;
        var category = CategoryCombo?.SelectedItem as string;

        var query = _allTags.AsEnumerable();
        if (!string.IsNullOrEmpty(category) && category != "(全部)")
            query = query.Where(t => string.Equals(t.Group, category, StringComparison.Ordinal));
        if (!string.IsNullOrEmpty(keyword))
            query = query.Where(t =>
                (t.Name?.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0) ||
                (t.NodeId?.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0));

        var filtered = query.OrderBy(t => t.Name, StringComparer.Ordinal).ToList();

        Roots.Clear();
        var rootDict = new Dictionary<string, TagTreeNode>(StringComparer.Ordinal);
        var autoExpand = !string.IsNullOrEmpty(keyword);

        foreach (var tag in filtered)
        {
            if (string.IsNullOrWhiteSpace(tag.Name)) continue;
            var segments = SplitPath(tag.Name!);
            if (segments.Count == 0) continue;

            // 第 1 段作为 root
            if (!rootDict.TryGetValue(segments[0], out var root))
            {
                root = new TagTreeNode { Name = segments[0], FullPath = segments[0] };
                rootDict[segments[0]] = root;
                Roots.Add(root);
            }

            var current = root;
            for (int i = 1; i < segments.Count; i++)
            {
                var seg = segments[i];
                var child = current.Children.FirstOrDefault(c => string.Equals(c.Name, seg, StringComparison.Ordinal));
                if (child is null)
                {
                    child = new TagTreeNode
                    {
                        Name = seg,
                        FullPath = current.FullPath + (seg.StartsWith("[") ? seg : "." + seg)
                    };
                    current.Children.Add(child);
                }
                current = child;
            }
            current.Tag = tag;
            current.TypeBadge = string.IsNullOrEmpty(tag.DataType) ? string.Empty : $"[{tag.DataType}]";
            current.ValueBadge = string.IsNullOrEmpty(tag.CurrentValue) ? string.Empty : $"= {tag.CurrentValue}";
            if (autoExpand) ExpandAncestors(root, current);
        }
    }

    /// <summary>把 "DB2002_Recipe.TypeData[0].AxisPosition_X1[3]" 切成 ["DB2002_Recipe","TypeData","[0]","AxisPosition_X1","[3]"]。</summary>
    private static List<string> SplitPath(string path)
    {
        var result = new List<string>();
        foreach (var seg in path.Split('.'))
        {
            if (string.IsNullOrEmpty(seg)) continue;
            // 把 "Foo[0][1]" 拆为 "Foo", "[0]", "[1]"
            var m = Regex.Matches(seg, @"^([^\[]+)|(\[[^\]]*\])");
            foreach (Match mm in m)
            {
                if (mm.Success && mm.Value.Length > 0) result.Add(mm.Value);
            }
        }
        return result;
    }

    private static bool ExpandAncestors(TagTreeNode root, TagTreeNode target)
    {
        if (ReferenceEquals(root, target)) return true;
        foreach (var c in root.Children)
        {
            if (ExpandAncestors(c, target))
            {
                root.IsExpanded = true;
                return true;
            }
        }
        return false;
    }

    private void TreeView_SelectionChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        SelectedPathText.Text = (e.NewValue as TagTreeNode)?.Tag is { } t ? t.NodeId ?? string.Empty : string.Empty;
    }

    private void TreeView_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e) => Confirm_Click(sender, new RoutedEventArgs());

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (TagTreeView.SelectedItem is TagTreeNode node && node.Tag is not null)
        {
            SelectedTagName = node.Tag.Name;
            DialogResult = true;
            Close();
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void ExpandAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var r in Roots) SetExpanded(r, true);
    }

    private void CollapseAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var r in Roots) SetExpanded(r, false);
    }

    private static void SetExpanded(TagTreeNode n, bool value)
    {
        n.IsExpanded = value;
        foreach (var c in n.Children) SetExpanded(c, value);
    }
}

public sealed class TagTreeNode : INotifyPropertyChanged
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public string TypeBadge { get; set; } = string.Empty;
    public string ValueBadge { get; set; } = string.Empty;
    public TagItem? Tag { get; set; }
    public ObservableCollection<TagTreeNode> Children { get; } = new();

    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        set { if (_isExpanded != value) { _isExpanded = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded))); } }
    }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set { if (_isSelected != value) { _isSelected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected))); } }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
