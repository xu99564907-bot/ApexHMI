using System.Windows;

namespace ApexHMI.Views.Dialogs;

public partial class OpcUaWriteTestDialog : Window
{
    public OpcUaWriteTestDialog(string targetNodeId)
    {
        InitializeComponent();
        TargetText.Text = targetNodeId;
        TargetNodeId = targetNodeId;
    }

    public string TargetNodeId { get; }
    public string NewValue { get; private set; } = string.Empty;

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ValueBox.Text))
        {
            MessageBox.Show("请输入写入值", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        NewValue = ValueBox.Text;
        DialogResult = true;
    }
}
