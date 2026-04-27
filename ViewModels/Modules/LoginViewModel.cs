using ApexHMI.Models;

namespace ApexHMI.ViewModels.Modules;

public sealed class LoginViewModel : ModuleViewModelBase
{
    public LoginViewModel(MainViewModel shell)
        : base(shell, "登录")
    {
    }

    public string LoginUser => Shell.LoginUser;
    public UserRole CurrentUserRole => Shell.CurrentUserRole;
    public string CurrentRoleText => Shell.CurrentRoleText;
    public bool CanEditParameters => Shell.CanEditParameters;
    public bool CanAdmin => Shell.CanAdmin;
}
