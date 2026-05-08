using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ApexHMI.Models;
using CommunityToolkit.Mvvm.Input;

namespace ApexHMI.ViewModels.Modules;

public sealed class LoginViewModel : ModuleViewModelBase
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public LoginViewModel(MainViewModel shell)
        : base(shell, "登录")
    {
        SwitchUserCommand = new RelayCommand(() => Shell.SwitchUserCommand.Execute(null));
        SavePermissionsCommand = new AsyncRelayCommand(SavePermissionsAsync);
        ReloadPermissionsCommand = new AsyncRelayCommand(LoadPermissionsAsync);
        SeedPermissions();
        _ = LoadPermissionsAsync();
    }

    public string LoginUser => Shell.LoginUser;
    public UserRole CurrentUserRole => Shell.CurrentUserRole;
    public string CurrentRoleText => Shell.CurrentRoleText;
    public bool CanEditParameters => Shell.CanEditParameters;
    public bool CanAdmin => Shell.CanAdmin;

    public IRelayCommand SwitchUserCommand { get; }
    public IAsyncRelayCommand SavePermissionsCommand { get; }
    public IAsyncRelayCommand ReloadPermissionsCommand { get; }

    /// <summary>L6: 角色权限说明（管理员可编辑并保存到 config/permissions.json）。</summary>
    public ObservableCollection<PermissionDescription> Permissions { get; } = new();

    private void SeedPermissions()
    {
        Permissions.Clear();
        Permissions.Add(new PermissionDescription { Role = "Operator", Description = "查看、监控、设备操作（手动单步、紧急停止）。" });
        Permissions.Add(new PermissionDescription { Role = "Engineer", Description = "可修改大部分工艺与设备参数；切换配方；触发临时提权流程。" });
        Permissions.Add(new PermissionDescription { Role = "Administrator", Description = "最高权限：维护系统级参数、复位报警、删除配方、归档履历。" });
    }

    public async Task LoadPermissionsAsync()
    {
        try
        {
            var path = Path.Combine(Shell.GetProjectRoot(), "config", "permissions.json");
            if (!File.Exists(path)) return;
            var json = await Compat.ReadAllTextAsync(path);
            var items = JsonSerializer.Deserialize<List<PermissionDescription>>(json, JsonOptions);
            if (items is null || items.Count == 0) return;
            Permissions.Clear();
            foreach (var p in items) Permissions.Add(p);
        }
        catch { /* keep seed */ }
    }

    public async Task SavePermissionsAsync()
    {
        if (!Shell.CanAdmin)
        {
            Shell.ShowPopup("权限不足", "仅管理员可编辑权限说明", "Warning");
            return;
        }
        var dir = Path.Combine(Shell.GetProjectRoot(), "config");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "permissions.json");
        var json = JsonSerializer.Serialize(Permissions.ToList(), JsonOptions);
        await Compat.WriteAllTextAsync(path, json);
        Shell.AddLog("登录", $"权限说明已保存：{path}", "Info");
        Shell.AddAudit("权限说明保存", path, "成功", $"共 {Permissions.Count} 条");
    }
}
