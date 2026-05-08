using System.Windows;

namespace ApexHMI.Views.Dialogs;

public partial class RecipeDeleteReasonDialog : Window
{
    public RecipeDeleteReasonDialog() => InitializeComponent();

    public string DeleteReason { get; private set; } = string.Empty;

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        DeleteReason = ReasonBox.Text;
        DialogResult = true;
    }
}
