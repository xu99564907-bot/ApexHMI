#nullable enable
using System.Windows;
using System.Windows.Controls;
using ApexHMI.Models.RuntimeUi;
using ApexHMI.Services.RuntimeUi;

namespace ApexHMI.Views.Dialogs;

/// <summary>P6E: 文本/图形列表资源编辑器。</summary>
public partial class ListResourceDialog : Window
{
    private readonly ListResources _lists;

    public ListResourceDialog(ListResources lists)
    {
        InitializeComponent();
        _lists = lists;
        TextListsList.ItemsSource = _lists.TextLists;
        GraphicListsList.ItemsSource = _lists.GraphicLists;
    }

    // ===== 文本列表 =====
    private void OnAddTextList(object sender, RoutedEventArgs e)
    {
        var lst = new TextList { Name = $"文本列表 {_lists.TextLists.Count + 1}" };
        _lists.TextLists.Add(lst);
        TextListsList.SelectedItem = lst;
    }

    private void OnRemoveTextList(object sender, RoutedEventArgs e)
    {
        if (TextListsList.SelectedItem is TextList lst) _lists.TextLists.Remove(lst);
    }

    private void OnTextListSelected(object sender, SelectionChangedEventArgs e)
    {
        if (TextListsList.SelectedItem is TextList lst)
        {
            TextListNameBox.Text = lst.Name;
            TextItemsGrid.ItemsSource = lst.Items;
        }
        else
        {
            TextListNameBox.Text = "";
            TextItemsGrid.ItemsSource = null;
        }
    }

    private void OnTextListNameChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (TextListsList.SelectedItem is TextList lst) lst.Name = TextListNameBox.Text ?? "";
    }

    private void OnAddTextItem(object sender, RoutedEventArgs e)
    {
        if (TextListsList.SelectedItem is TextList lst) lst.Items.Add(new TextListItem());
    }

    private void OnRemoveTextItem(object sender, RoutedEventArgs e)
    {
        if (TextListsList.SelectedItem is TextList lst && TextItemsGrid.SelectedItem is TextListItem it)
            lst.Items.Remove(it);
    }

    // ===== 图形列表 =====
    private void OnAddGraphicList(object sender, RoutedEventArgs e)
    {
        var lst = new GraphicList { Name = $"图形列表 {_lists.GraphicLists.Count + 1}" };
        _lists.GraphicLists.Add(lst);
        GraphicListsList.SelectedItem = lst;
    }

    private void OnRemoveGraphicList(object sender, RoutedEventArgs e)
    {
        if (GraphicListsList.SelectedItem is GraphicList lst) _lists.GraphicLists.Remove(lst);
    }

    private void OnGraphicListSelected(object sender, SelectionChangedEventArgs e)
    {
        if (GraphicListsList.SelectedItem is GraphicList lst)
        {
            GraphicListNameBox.Text = lst.Name;
            GraphicItemsGrid.ItemsSource = lst.Items;
        }
        else
        {
            GraphicListNameBox.Text = "";
            GraphicItemsGrid.ItemsSource = null;
        }
    }

    private void OnGraphicListNameChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (GraphicListsList.SelectedItem is GraphicList lst) lst.Name = GraphicListNameBox.Text ?? "";
    }

    private void OnAddGraphicItem(object sender, RoutedEventArgs e)
    {
        if (GraphicListsList.SelectedItem is GraphicList lst) lst.Items.Add(new GraphicListItem());
    }

    private void OnRemoveGraphicItem(object sender, RoutedEventArgs e)
    {
        if (GraphicListsList.SelectedItem is GraphicList lst && GraphicItemsGrid.SelectedItem is GraphicListItem it)
            lst.Items.Remove(it);
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
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
