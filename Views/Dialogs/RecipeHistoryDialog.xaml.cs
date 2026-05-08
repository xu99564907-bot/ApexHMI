using System.Windows;
using ApexHMI.Models;

namespace ApexHMI.Views.Dialogs;

public partial class RecipeHistoryDialog : Window
{
    public RecipeHistoryDialog() => InitializeComponent();

    public RecipeSnapshot? RestoreSnapshot { get; private set; }

    private void Restore_Click(object sender, RoutedEventArgs e)
    {
        if (HistoryGrid.SelectedItem is not RecipeSnapshot snap)
        {
            MessageBox.Show("请先在列表中选择要回滚的版本", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (MessageBox.Show($"确认回滚到 {snap.Timestamp:yyyy-MM-dd HH:mm:ss}？", "确认", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }
        RestoreSnapshot = snap;
        DialogResult = true;
    }
}
