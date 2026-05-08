using System.Windows;
using System.Windows.Controls;

namespace ApexHMI.Views.Dialogs;

public partial class SwitchUserDialog : Window
{
    public SwitchUserDialog() => InitializeComponent();

    public string SelectedRole { get; private set; } = string.Empty;
    public string Password { get; private set; } = string.Empty;

    public sealed class SwitchUserViewModel
    {
        public string Title { get; set; } = "请输入新角色与密码：";
        public string Hint { get; set; } = "登录密码可在 config/users.json 中维护。";
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (RoleCombo.SelectedItem is not ComboBoxItem item)
        {
            MessageBox.Show("请先选择角色", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        SelectedRole = item.Content?.ToString() ?? string.Empty;
        Password = PwdBox.Password;
        DialogResult = true;
    }
}
