using CommunityToolkit.Mvvm.ComponentModel;

namespace ApexHMI.Models;

/// <summary>L6: 角色权限说明（可被管理员编辑）。持久化到 config/permissions.json。</summary>
public partial class PermissionDescription : ObservableObject
{
    [ObservableProperty]
    private string role = string.Empty;

    [ObservableProperty]
    private string description = string.Empty;
}
