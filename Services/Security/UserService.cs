using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ApexHMI.Models;
using Microsoft.Extensions.Options;
using Serilog;

namespace ApexHMI.Services.Security;

/// <summary>
/// 文件持久化的用户管理实现。读写 config/users.json。
/// 第一次启动若文件不存在，会用默认 3 角色（operator/engineer/admin）+ 旧硬编码密码生成迁移数据。
/// <para>M6.1: 账户锁定不再由本服务保存到 UserAccount.LockedUntil，已迁到
/// <see cref="AccountLockoutService"/>（唯一来源）。本服务只在 Authenticate 时调三个 API。
/// 注意：AccountLockoutService 通过属性注入（构造函数可选），未注入时退化为不锁定（向后兼容旧测试）。</para>
/// </summary>
public class UserService : IUserService
{
    private const string UsersFileName = "users.json";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _configPath;
    private readonly object _lock = new();
    private readonly List<UserAccount> _users;

    /// <summary>M6.1: 账户锁定服务（属性注入，可选）。Bootstrapper 构造后由 DI 容器/Shell 注入。</summary>
    public AccountLockoutService? AccountLockout { get; set; }

    public UserService(IOptions<AppOptions> options)
    {
        var configDirName = options?.Value?.ConfigFiles?.ConfigDirectoryName ?? "config";
        var configDir = Path.Combine(AppContext.BaseDirectory, configDirName);
        Directory.CreateDirectory(configDir);
        _configPath = Path.Combine(configDir, UsersFileName);
        _users = LoadOrCreateDefault();
    }

    private List<UserAccount> LoadOrCreateDefault()
    {
        if (File.Exists(_configPath))
        {
            try
            {
                var json = File.ReadAllText(_configPath);
                var users = JsonSerializer.Deserialize<List<UserAccount>>(json) ?? new List<UserAccount>();
                if (users.Count > 0)
                {
                    // M6.3: 旧用户记录无 PasswordChangedAt，加载时初始化为 UtcNow（即被视为刚改密的状态）
                    var changed = false;
                    foreach (var u in users)
                    {
                        if (u.PasswordChangedAt is null)
                        {
                            u.PasswordChangedAt = DateTime.UtcNow;
                            changed = true;
                        }
                    }
                    if (changed)
                    {
                        try { SaveTo(_configPath, users); } catch { /* ignore */ }
                    }
                    return users;
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "users.json 读取失败，使用默认账号。Path={Path}", _configPath);
            }
        }

        var defaults = CreateDefaults();
        SaveTo(_configPath, defaults);
        Log.Information("UserService: 首次启动，已生成默认 users.json。Path={Path}", _configPath);
        return defaults;
    }

    private static List<UserAccount> CreateDefaults()
    {
        return new List<UserAccount>
        {
            CreateUser("operator", UserRole.Operator, ""),
            CreateUser("engineer", UserRole.Engineer, "123456"),
            CreateUser("admin", UserRole.Administrator, "admin888"),
        };
    }

    private static UserAccount CreateUser(string username, UserRole role, string password)
    {
        var salt = GenerateSalt();
        return new UserAccount
        {
            Username = username,
            Role = role,
            Salt = salt,
            PasswordHash = string.IsNullOrEmpty(password) ? string.Empty : Hash(password, salt),
            DisplayName = string.Empty,
            PasswordChangedAt = DateTime.UtcNow,
        };
    }

    private static string GenerateSalt()
    {
        var bytes = new byte[16];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }

    private static string Hash(string password, string salt)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(salt + password);
        return Convert.ToBase64String(sha.ComputeHash(bytes));
    }

    private static bool VerifyPassword(UserAccount user, string password)
    {
        if (string.IsNullOrEmpty(user.PasswordHash))
            return string.IsNullOrEmpty(password);
        return string.Equals(Hash(password, user.Salt), user.PasswordHash, StringComparison.Ordinal);
    }

    public UserAccount? Authenticate(string? username, string? password)
    {
        if (string.IsNullOrEmpty(username)) return null;
        lock (_lock)
        {
            var user = _users.FirstOrDefault(u =>
                string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));
            return AuthenticateInternal(user, password ?? string.Empty);
        }
    }

    public UserAccount? AuthenticateByRole(string? roleName, string? password)
    {
        var role = roleName switch
        {
            "Operator" => (UserRole?)UserRole.Operator,
            "Engineer" => UserRole.Engineer,
            "Administrator" => UserRole.Administrator,
            _ => null,
        };
        if (role is null) return null;

        lock (_lock)
        {
            var user = _users.FirstOrDefault(u => u.Role == role.Value);
            return AuthenticateInternal(user, password ?? string.Empty);
        }
    }

    private UserAccount? AuthenticateInternal(UserAccount? user, string password)
    {
        if (user is null) return null;

        // M6.1: 锁定状态由 AccountLockoutService 唯一裁定
        if (AccountLockout is not null && AccountLockout.IsLocked(user.Username))
        {
            Log.Warning("UserService: 用户 {U} 当前处于锁定状态，登录被拒", user.Username);
            return null;
        }

        if (!VerifyPassword(user, password))
        {
            AccountLockout?.RegisterFailure(user.Username);
            user.FailedAttempts = AccountLockout?.GetFailureCount(user.Username) ?? (user.FailedAttempts + 1);
            SaveTo(_configPath, _users);
            return null;
        }

        user.LastLoginAt = DateTime.Now;
        user.FailedAttempts = 0;
        AccountLockout?.ResetCounter(user.Username);
        SaveTo(_configPath, _users);
        return user;
    }

    public IReadOnlyList<UserAccount> ListUsers()
    {
        lock (_lock) return _users.ToList();
    }

    public bool ChangePassword(string username, string newPassword)
    {
        lock (_lock)
        {
            var user = _users.FirstOrDefault(u =>
                string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));
            if (user is null) return false;
            user.Salt = GenerateSalt();
            user.PasswordHash = string.IsNullOrEmpty(newPassword) ? string.Empty : Hash(newPassword, user.Salt);
            user.PasswordChangedAt = DateTime.UtcNow;
            SaveTo(_configPath, _users);
            return true;
        }
    }

    public bool AddUser(string username, string password, UserRole role)
    {
        if (string.IsNullOrWhiteSpace(username)) return false;
        lock (_lock)
        {
            if (_users.Any(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase)))
                return false;
            _users.Add(CreateUser(username.Trim(), role, password ?? string.Empty));
            SaveTo(_configPath, _users);
            Log.Information("UserService: 已新增用户 {Username} 角色 {Role}", username, role);
            return true;
        }
    }

    public bool RemoveUser(string username)
    {
        if (string.IsNullOrWhiteSpace(username)) return false;
        if (string.Equals(username, "admin", StringComparison.OrdinalIgnoreCase))
        {
            Log.Warning("UserService: 拒绝删除内置 admin 账号");
            return false;
        }
        lock (_lock)
        {
            var user = _users.FirstOrDefault(u =>
                string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));
            if (user is null) return false;
            _users.Remove(user);
            SaveTo(_configPath, _users);
            Log.Information("UserService: 已删除用户 {Username}", username);
            return true;
        }
    }

    public bool SetUserRole(string username, UserRole role)
    {
        if (string.IsNullOrWhiteSpace(username)) return false;
        lock (_lock)
        {
            var user = _users.FirstOrDefault(u =>
                string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));
            if (user is null) return false;
            user.Role = role;
            SaveTo(_configPath, _users);
            Log.Information("UserService: 用户 {Username} 角色改为 {Role}", username, role);
            return true;
        }
    }

    public void Save()
    {
        lock (_lock) SaveTo(_configPath, _users);
    }

    private static void SaveTo(string path, List<UserAccount> users)
    {
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, JsonSerializer.Serialize(users, JsonOptions));
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "users.json 写入失败：{Path}", path);
        }
    }
}
