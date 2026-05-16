#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ApexHMI.Models;
using Microsoft.Extensions.Options;
using Serilog;

namespace ApexHMI.Services.Security;

/// <summary>M5.2: 密码策略配置（持久化 config/password-policy.json）。</summary>
public sealed class PasswordPolicyConfig
{
    public int MinLength { get; set; } = 8;
    public bool RequireDigits { get; set; } = true;
    public bool RequireLetters { get; set; } = true;
    public bool RequireSpecial { get; set; } = false;
    /// <summary>历史不重复条数（最近 N 次）。0 = 关闭。</summary>
    public int HistoryCount { get; set; } = 3;
    /// <summary>M6.3: 密码最长有效期（天）。0 = 永不过期，默认 90。</summary>
    public int MaxAgeDays { get; set; } = 90;
    /// <summary>M6.3: 接近过期的提醒阈值（天）。默认 7（剩余 ≤ 7 天提醒）。</summary>
    public int WarnDaysBeforeExpire { get; set; } = 7;
    /// <summary>每个用户的密码 hash 历史（最近 HistoryCount 条）— 由 PasswordPolicy 内部维护。</summary>
    public Dictionary<string, List<string>> UserHistory { get; set; } = new();
}

/// <summary>M6.3: 密码过期状态。</summary>
public enum PasswordExpirationState
{
    /// <summary>未启用过期或未到提醒阈值。</summary>
    Healthy,
    /// <summary>临近过期（剩余 ≤ WarnDaysBeforeExpire 天）。</summary>
    NearExpiry,
    /// <summary>已过期 — 调用方应强制改密。</summary>
    Expired,
}

/// <summary>M5.2: 密码策略校验结果。</summary>
public readonly record struct PasswordPolicyResult(bool IsValid, IReadOnlyList<string> Errors)
{
    public static PasswordPolicyResult Ok() => new(true, Array.Empty<string>());
    public static PasswordPolicyResult Fail(params string[] errors) => new(false, errors);
}

/// <summary>
/// M5.2: 密码策略服务。
/// <para>校验密码长度 / 复杂度 / 历史不重复，并记录每用户最近 N 次密码 hash。</para>
/// </summary>
public sealed class PasswordPolicy
{
    private readonly string _configPath;
    private readonly object _lock = new();
    private PasswordPolicyConfig _cfg;

    public PasswordPolicy(IOptions<AppOptions>? options = null)
    {
        var configDirName = options?.Value?.ConfigFiles?.ConfigDirectoryName ?? "config";
        var configDir = Path.Combine(AppContext.BaseDirectory, configDirName);
        Directory.CreateDirectory(configDir);
        _configPath = Path.Combine(configDir, "password-policy.json");
        _cfg = Load();
    }

    public PasswordPolicyConfig Config
    {
        get { lock (_lock) return _cfg; }
    }

    public void UpdateConfig(Action<PasswordPolicyConfig> mutate)
    {
        lock (_lock)
        {
            mutate(_cfg);
            Save();
        }
    }

    /// <summary>校验密码是否符合策略。<paramref name="username"/> 用于历史不重复检查。</summary>
    public PasswordPolicyResult Validate(string username, string password)
    {
        var errors = new List<string>();
        lock (_lock)
        {
            if (string.IsNullOrEmpty(password))
            {
                errors.Add("密码不能为空");
                return PasswordPolicyResult.Fail(errors.ToArray());
            }
            if (password.Length < _cfg.MinLength)
                errors.Add($"密码至少 {_cfg.MinLength} 位");
            if (_cfg.RequireDigits && !password.Any(char.IsDigit))
                errors.Add("密码必须包含数字");
            if (_cfg.RequireLetters && !password.Any(char.IsLetter))
                errors.Add("密码必须包含字母");
            if (_cfg.RequireSpecial && password.All(char.IsLetterOrDigit))
                errors.Add("密码必须包含特殊字符");
            if (_cfg.HistoryCount > 0 && !string.IsNullOrEmpty(username)
                && _cfg.UserHistory.TryGetValue(username, out var hist))
            {
                var hash = SimpleHash(password);
                if (hist.Contains(hash))
                    errors.Add($"密码不可与最近 {_cfg.HistoryCount} 次相同");
            }
        }
        return errors.Count == 0 ? PasswordPolicyResult.Ok() : PasswordPolicyResult.Fail(errors.ToArray());
    }

    /// <summary>M6.3: 检查 <paramref name="passwordChangedAt"/> 距今的天数是否超出策略。</summary>
    public PasswordExpirationState CheckExpiration(DateTime? passwordChangedAt)
    {
        lock (_lock)
        {
            if (_cfg.MaxAgeDays <= 0) return PasswordExpirationState.Healthy;
            var changedAt = passwordChangedAt ?? DateTime.UtcNow;
            var age = (DateTime.UtcNow - changedAt).TotalDays;
            if (age >= _cfg.MaxAgeDays) return PasswordExpirationState.Expired;
            if (age >= _cfg.MaxAgeDays - Math.Max(0, _cfg.WarnDaysBeforeExpire))
                return PasswordExpirationState.NearExpiry;
            return PasswordExpirationState.Healthy;
        }
    }

    /// <summary>M6.3: 返回密码剩余有效天数（负数 = 已过期）。MaxAgeDays = 0 时返回 int.MaxValue。</summary>
    public int RemainingDays(DateTime? passwordChangedAt)
    {
        lock (_lock)
        {
            if (_cfg.MaxAgeDays <= 0) return int.MaxValue;
            var changedAt = passwordChangedAt ?? DateTime.UtcNow;
            var age = (DateTime.UtcNow - changedAt).TotalDays;
            return (int)Math.Ceiling(_cfg.MaxAgeDays - age);
        }
    }

    /// <summary>修改密码成功后调用：把新密码 hash 加入历史，自动裁剪到 HistoryCount。</summary>
    public void RecordPasswordChange(string username, string newPassword)
    {
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(newPassword)) return;
        lock (_lock)
        {
            if (!_cfg.UserHistory.TryGetValue(username, out var hist))
            {
                hist = new List<string>();
                _cfg.UserHistory[username] = hist;
            }
            hist.Add(SimpleHash(newPassword));
            while (hist.Count > Math.Max(1, _cfg.HistoryCount)) hist.RemoveAt(0);
            Save();
        }
    }

    private static string SimpleHash(string s)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(s));
        return Convert.ToBase64String(bytes);
    }

    private PasswordPolicyConfig Load()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                var cfg = JsonSerializer.Deserialize<PasswordPolicyConfig>(json);
                if (cfg is not null) return cfg;
            }
        }
        catch (Exception ex) { Log.Warning(ex, "PasswordPolicy: 配置读取失败 path={Path}", _configPath); }
        var def = new PasswordPolicyConfig();
        try { File.WriteAllText(_configPath, JsonSerializer.Serialize(def, new JsonSerializerOptions { WriteIndented = true })); }
        catch (Exception ex) { Log.Warning(ex, "PasswordPolicy: 默认配置写入失败"); }
        return def;
    }

    private void Save()
    {
        try { File.WriteAllText(_configPath, JsonSerializer.Serialize(_cfg, new JsonSerializerOptions { WriteIndented = true })); }
        catch (Exception ex) { Log.Warning(ex, "PasswordPolicy: 写入失败"); }
    }
}
