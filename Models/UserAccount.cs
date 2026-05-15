using System;
using System.Text.Json.Serialization;

namespace ApexHMI.Models;

/// <summary>
/// 用户账号记录。落盘到 config/users.json，密码以 SHA256(salt + password) 单向哈希存储。
/// </summary>
public class UserAccount
{
    public string Username { get; set; } = string.Empty;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public UserRole Role { get; set; } = UserRole.Operator;

    /// <summary>Base64 编码的 SHA256 哈希。空表示该账号无密码（默认 operator）。</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Base64 编码的随机盐，每次创建用户/改密时重新生成。</summary>
    public string Salt { get; set; } = string.Empty;

    public DateTime? LastLoginAt { get; set; }

    /// <summary>
    /// 兼容字段：M5.2 曾在此累计失败次数，M6.1 后由 <see cref="ApexHMI.Services.Security.AccountLockoutService"/>
    /// 维护内存计数。本字段仅在 users.json 序列化兼容时保留，运行时不再更新。
    /// UI 显示请通过 AccountLockoutService.GetFailureCount(username) 查询。
    /// </summary>
    public int FailedAttempts { get; set; }

    /// <summary>显示名（可选，留空则用 Username）。</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>M6.3: 上次密码修改时间。空表示从未记录（旧用户加载时初始化为 UtcNow）。</summary>
    public DateTime? PasswordChangedAt { get; set; }
}
