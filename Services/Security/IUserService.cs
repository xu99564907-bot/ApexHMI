using System.Collections.Generic;
using ApexHMI.Models;

namespace ApexHMI.Services.Security;

/// <summary>
/// 用户管理服务。封装 config/users.json 的读写、密码哈希校验、登录记录。
/// </summary>
public interface IUserService
{
    /// <summary>按用户名 + 密码登录。失败返回 null（密码错 / 用户不存在 / 账号锁定）。</summary>
    UserAccount? Authenticate(string? username, string? password);

    /// <summary>
    /// 按角色名（"Operator" / "Engineer" / "Administrator"）取该角色的默认账号并校验密码。
    /// 兼容当前 LoginView "三个角色按钮" 的登录习惯：用户不输入用户名，只选角色 + 输密码。
    /// </summary>
    UserAccount? AuthenticateByRole(string? roleName, string? password);

    /// <summary>所有用户列表（只读快照）。</summary>
    IReadOnlyList<UserAccount> ListUsers();

    /// <summary>修改某用户的密码。返回是否成功。</summary>
    bool ChangePassword(string username, string newPassword);

    /// <summary>P10B: 新增用户。重名或参数不合法时返回 false。</summary>
    bool AddUser(string username, string password, UserRole role);

    /// <summary>P10B: 删除用户。用户不存在时返回 false；admin 账号禁止删除。</summary>
    bool RemoveUser(string username);

    /// <summary>P10B: 切换用户角色。用户不存在时返回 false。</summary>
    bool SetUserRole(string username, UserRole role);

    /// <summary>立即把当前用户列表落盘（一般在 Authenticate 内部已自动落盘，仅在外部强制持久化时需要）。</summary>
    void Save();
}
