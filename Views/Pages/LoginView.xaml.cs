using System.Windows.Controls;
using ApexHMI.ViewModels.Modules;

namespace ApexHMI.Views.Pages;

public partial class LoginView : UserControl
{
    public LoginView()
    {
        InitializeComponent();
    }

    private void RolePasswordBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel vm && sender is PasswordBox passwordBox)
        {
            vm.ShellViewModel.LoginPassword = passwordBox.Password;
        }
    }
}
