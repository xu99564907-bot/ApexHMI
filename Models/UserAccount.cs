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

    public int FailedAttempts { get; set; }

    public DateTime? LockedUntil { get; set; }

    /// <summary>显示名（可选，留空则用 Username）。</summary>
    public string DisplayName { get; set; } = string.Empty;
}
