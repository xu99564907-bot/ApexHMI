#nullable enable
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ApexHMI.Models;
using ApexHMI.Models.RuntimeUi;
using ApexHMI.Services.RuntimeUi;
using ApexHMI.Services.Security;
using ApexHMI.Services;

namespace ApexHMI.ViewModels.Runtime;

/// <summary>
/// P8B 用户视图：基于 <see cref="IUserService"/>（通过反射从 Shell 取，避免循环依赖）。
/// <para>支持：浏览用户列表 / 修改密码。</para>
/// <para>新增/删除用户在当前 <see cref="IUserService"/> API 中尚未提供 — 按钮置灰并提示。</para>
/// </summary>
public partial class UserViewWidgetViewModel : WidgetViewModelBase
{
    private IUserService? _userService;
    private PasswordPolicy? _passwordPolicy; // M5.2

    public UserViewWidgetViewModel(WidgetInstance model, IWidgetDataContext dataContext)
        : base(model, dataContext)
    {
        ResolveUserService();
        Refresh();
    }

    public ObservableCollection<UserAccount> Users { get; } = new();

    [ObservableProperty] private UserAccount? _selectedUser;

    public bool AllowEdit => string.Equals(Prop("allowEdit", "true"), "true", StringComparison.OrdinalIgnoreCase);
    public bool ShowLastLogin => string.Equals(Prop("showLastLogin", "true"), "true", StringComparison.OrdinalIgnoreCase);
    public string Background => Prop("background", "#FFFFFF");
    public string Foreground => Prop("foreground", "#0F172A");

    private void ResolveUserService()
    {
        // 先从 Shell 反射 UserService 属性；不存在则回退到 App.ServiceProvider
        var shell = _dataContext.Shell;
        if (shell is null) return;
        var t = shell.GetType();
        // 1) 公开属性 UserService
        var prop = t.GetProperty("UserService", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
        if (prop?.GetValue(shell) is IUserService svc)
        {
            _userService = svc;
            return;
        }
        // 2) 私有字段 _userService
        var field = t.GetField("_userService", BindingFlags.Instance | BindingFlags.NonPublic);
        if (field?.GetValue(shell) is IUserService svc2)
        {
            _userService = svc2;
        }
        // M5.2: 反射拿 PasswordPolicy（供 ChangePassword / AddUser 时校验）
        var ppProp = t.GetProperty("PasswordPolicy", BindingFlags.Public | BindingFlags.Instance);
        if (ppProp?.GetValue(shell) is PasswordPolicy pp) _passwordPolicy = pp;
    }

    [RelayCommand]
    private void Refresh()
    {
        Users.Clear();
        if (_userService is null)
        {
            // 设计时占位
            Users.Add(new UserAccount { Username = "(设计时占位)", Role = UserRole.Operator });
            return;
        }
        foreach (var u in _userService.ListUsers())
        {
            Users.Add(u);
        }
        SelectedUser = Users.FirstOrDefault();
    }

    [RelayCommand]
    private void ChangePassword()
    {
        if (!AllowEdit || _userService is null || SelectedUser is null) return;
        var dlg = new PasswordPromptWindow(SelectedUser.Username);
        if (dlg.ShowDialog() != true) return;
        // M5.2: 密码策略校验
        if (_passwordPolicy is not null)
        {
            var r = _passwordPolicy.Validate(SelectedUser.Username, dlg.PasswordText);
            if (!r.IsValid)
            {
                MessageBox.Show("密码不符合策略：\n - " + string.Join("\n - ", r.Errors),
                    "密码策略", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }
        if (_userService.ChangePassword(SelectedUser.Username, dlg.PasswordText))
        {
            _passwordPolicy?.RecordPasswordChange(SelectedUser.Username, dlg.PasswordText);
            MessageBox.Show($"已修改 {SelectedUser.Username} 的密码。", "用户管理");
        }
        else
        {
            MessageBox.Show("修改密码失败。", "用户管理", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void AddUser()
    {
        if (!AllowEdit || _userService is null) return;
        var dlg = new AddUserPromptWindow();
        if (dlg.ShowDialog() != true) return;
        if (string.IsNullOrWhiteSpace(dlg.UsernameText))
        {
            MessageBox.Show("用户名不能为空。", "用户管理", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (_userService.AddUser(dlg.UsernameText.Trim(), dlg.PasswordText, dlg.SelectedRole))
        {
            MessageBox.Show($"已新增用户 {dlg.UsernameText.Trim()}。", "用户管理");
            Refresh();
        }
        else
        {
            MessageBox.Show("新增失败（重名或非法）。", "用户管理", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void RemoveUser()
    {
        if (!AllowEdit || _userService is null || SelectedUser is null) return;
        if (MessageBox.Show($"确认删除用户 {SelectedUser.Username}？",
                "用户管理", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
        if (_userService.RemoveUser(SelectedUser.Username))
        {
            Refresh();
        }
        else
        {
            MessageBox.Show("删除失败（admin 账号不可删除）。", "用户管理", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void SwitchRole()
    {
        if (!AllowEdit || _userService is null || SelectedUser is null) return;
        var dlg = new RolePromptWindow(SelectedUser.Username, SelectedUser.Role);
        if (dlg.ShowDialog() != true) return;
        if (_userService.SetUserRole(SelectedUser.Username, dlg.SelectedRole))
        {
            MessageBox.Show($"{SelectedUser.Username} 角色改为 {dlg.SelectedRole}。", "用户管理");
            Refresh();
        }
    }
}

/// <summary>P10B: 新增用户弹窗。</summary>
internal sealed class AddUserPromptWindow : Window
{
    private readonly System.Windows.Controls.TextBox _user = new() { Width = 220, Margin = new Thickness(8) };
    private readonly System.Windows.Controls.PasswordBox _pwd = new() { Width = 220, Margin = new Thickness(8) };
    private readonly System.Windows.Controls.ComboBox _role = new() { Width = 220, Margin = new Thickness(8) };

    public string UsernameText => _user.Text ?? string.Empty;
    public string PasswordText => _pwd.Password ?? string.Empty;
    public UserRole SelectedRole => (UserRole)(_role.SelectedItem ?? UserRole.Operator);

    public AddUserPromptWindow()
    {
        Title = "新增用户";
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;

        _role.ItemsSource = new[] { UserRole.Operator, UserRole.Engineer, UserRole.Administrator };
        _role.SelectedIndex = 0;

        var stack = new System.Windows.Controls.StackPanel { Margin = new Thickness(12) };
        stack.Children.Add(new System.Windows.Controls.TextBlock { Text = "用户名：" });
        stack.Children.Add(_user);
        stack.Children.Add(new System.Windows.Controls.TextBlock { Text = "密码：" });
        stack.Children.Add(_pwd);
        stack.Children.Add(new System.Windows.Controls.TextBlock { Text = "角色：" });
        stack.Children.Add(_role);

        var btns = new System.Windows.Controls.StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 8, 0, 0),
        };
        var ok = new System.Windows.Controls.Button { Content = "确定", Width = 70, Margin = new Thickness(4), IsDefault = true };
        var cancel = new System.Windows.Controls.Button { Content = "取消", Width = 70, Margin = new Thickness(4), IsCancel = true };
        ok.Click += (_, _) => { DialogResult = true; Close(); };
        btns.Children.Add(ok);
        btns.Children.Add(cancel);
        stack.Children.Add(btns);
        Content = stack;
    }
}

/// <summary>P10B: 切换角色弹窗。</summary>
internal sealed class RolePromptWindow : Window
{
    private readonly System.Windows.Controls.ComboBox _role = new() { Width = 220, Margin = new Thickness(8) };
    public UserRole SelectedRole => (UserRole)(_role.SelectedItem ?? UserRole.Operator);

    public RolePromptWindow(string username, UserRole current)
    {
        Title = $"切换角色 — {username}";
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;

        _role.ItemsSource = new[] { UserRole.Operator, UserRole.Engineer, UserRole.Administrator };
        _role.SelectedItem = current;

        var stack = new System.Windows.Controls.StackPanel { Margin = new Thickness(12) };
        stack.Children.Add(new System.Windows.Controls.TextBlock { Text = $"为 {username} 选择新角色：", Margin = new Thickness(8, 0, 8, 4) });
        stack.Children.Add(_role);

        var btns = new System.Windows.Controls.StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 8, 0, 0),
        };
        var ok = new System.Windows.Controls.Button { Content = "确定", Width = 70, Margin = new Thickness(4), IsDefault = true };
        var cancel = new System.Windows.Controls.Button { Content = "取消", Width = 70, Margin = new Thickness(4), IsCancel = true };
        ok.Click += (_, _) => { DialogResult = true; Close(); };
        btns.Children.Add(ok);
        btns.Children.Add(cancel);
        stack.Children.Add(btns);
        Content = stack;
    }
}

/// <summary>简易密码输入弹窗（P8B 用户视图改密用）。</summary>
internal sealed class PasswordPromptWindow : Window
{
    private readonly System.Windows.Controls.PasswordBox _pwd = new() { Width = 220, Margin = new Thickness(8) };
    public string PasswordText => _pwd.Password ?? string.Empty;

    public PasswordPromptWindow(string username)
    {
        Title = $"修改密码 — {username}";
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;

        var stack = new System.Windows.Controls.StackPanel { Margin = new Thickness(12) };
        stack.Children.Add(new System.Windows.Controls.TextBlock { Text = $"为 {username} 设置新密码：", Margin = new Thickness(8, 0, 8, 4) });
        stack.Children.Add(_pwd);

        var btns = new System.Windows.Controls.StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 8, 0, 0),
        };
        var ok = new System.Windows.Controls.Button { Content = "确定", Width = 70, Margin = new Thickness(4), IsDefault = true };
        var cancel = new System.Windows.Controls.Button { Content = "取消", Width = 70, Margin = new Thickness(4), IsCancel = true };
        ok.Click += (_, _) => { DialogResult = true; Close(); };
        btns.Children.Add(ok);
        btns.Children.Add(cancel);
        stack.Children.Add(btns);
        Content = stack;
    }
}
