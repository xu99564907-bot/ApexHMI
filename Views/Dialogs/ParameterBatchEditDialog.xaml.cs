using System.Windows;

namespace ApexHMI.Views.Dialogs;

public partial class ParameterBatchEditDialog : Window
{
    public ParameterBatchEditDialog() => InitializeComponent();

    public string? NewValue { get; private set; }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(ValueBox.Text))
        {
            MessageBox.Show("请填写新值", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        NewValue = ValueBox.Text;
        DialogResult = true;
    }
}
