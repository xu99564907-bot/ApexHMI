using System.Windows;

namespace ApexHMI.Views.Dialogs;

public partial class RecipeTrialRunDialog : Window
{
    public RecipeTrialRunDialog() => InitializeComponent();

    public int Quantity { get; private set; }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(QtyBox.Text, out var n) || n <= 0)
        {
            MessageBox.Show("请填写大于 0 的整数件数", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        Quantity = n;
        DialogResult = true;
    }
}
