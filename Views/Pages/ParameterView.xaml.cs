using System.Windows;
using System.Windows.Controls;
using ApexHMI.ViewModels.Modules;
using ApexHMI.Views.Dialogs;

namespace ApexHMI.Views.Pages;

public partial class ParameterView : UserControl
{
    public ParameterView()
    {
        InitializeComponent();
    }

    private void OpenCommunicationConfigButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ParameterViewModel vm)
        {
            return;
        }

        var owner = Window.GetWindow(this);
        var window = new CommunicationConfigWindow
        {
            Owner = owner,
            DataContext = vm.ShellViewModel
        };
        window.ShowDialog();
    }

    private async void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ParameterViewModel vm)
        {
            await vm.ShellViewModel.ConnectCommand.ExecuteAsync(null);
        }
    }

    private async void DisconnectButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ParameterViewModel vm)
        {
            await vm.ShellViewModel.DisconnectCommand.ExecuteAsync(null);
        }
    }
}
