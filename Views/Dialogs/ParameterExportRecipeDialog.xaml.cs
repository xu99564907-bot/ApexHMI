using System.Windows;

namespace ApexHMI.Views.Dialogs;

public partial class ParameterExportRecipeDialog : Window
{
    public ParameterExportRecipeDialog() => InitializeComponent();

    public string RecipeName { get; private set; } = string.Empty;

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            MessageBox.Show("请输入配方名称", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        RecipeName = NameBox.Text;
        DialogResult = true;
    }
}
