#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ApexHMI.Interfaces;
using Serilog;

namespace ApexHMI.Services.Security;

/// <summary>
/// M5.2: 账户锁定服务。跟踪每用户连续登录失败次数，超阈值则锁定一段时间。
/// <para>锁定状态本身持久化在 <see cref="UserAccount.LockedUntil"/>；本服务负责次数累计 + 阈值判定 + 审计写。</para>
/// </summary>
public sealed class AccountLockoutService
{
    private readonly IAuditService _audit;
    private readonly object _lock = new();
    private readonly Dictionary<string, int> _failsByUser = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>连续失败几次后锁定（默认 5）。</summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>锁定持续时间（默认 15 分钟）。</summary>
    public TimeSpan LockoutDuration { get; set; } = TimeSpan.FromMinutes(15);

    public AccountLockoutService(IAuditService audit)
    {
        _audit = audit;
    }

    /// <summary>登录前调用：返回 true 表示账号当前处于锁定，应直接拒绝。</summary>
    public bool IsLocked(string username, DateTime? lockedUntil)
    {
        if (string.IsNullOrEmpty(username)) return false;
        if (lockedUntil is { } u && u > DateTime.Now) return true;
        return false;
    }

    /// <summary>登录失败后调用。返回 true 表示本次失败已触发锁定，调用方应同时设置 <see cref="UserAccount.LockedUntil"/>。</summary>
    public bool RegisterFailure(string username)
    {
        if (string.IsNullOrEmpty(username)) return false;
        int n;
        lock (_lock)
        {
            _failsByUser.TryGetValue(username, out var prior);
            n = prior + 1;
            _failsByUser[username] = n;
        }
        if (n >= MaxAttempts)
        {
            _ = _audit.LogOperationAsync(username, "account-locked", username, false,
                $"连续失败 {n} 次，锁定 {LockoutDuration.TotalMinutes:F0} 分钟");
            Log.Warning("AccountLockout: 用户 {User} 因连续失败 {N} 次被锁定", username, n);
            lock (_lock) _failsByUser[username] = 0;
            return true;
        }
        return false;
    }

    /// <summary>登录成功后调用：清空累计计数。</summary>
    public void RegisterSuccess(string username)
    {
        if (string.IsNullOrEmpty(username)) return;
        lock (_lock) _failsByUser.Remove(username);
    }

    /// <summary>管理员手工解锁。</summary>
    public Task UnlockAsync(string username, string operatorUser)
    {
        lock (_lock) _failsByUser.Remove(username);
        return _audit.LogOperationAsync(operatorUser, "account-unlock", username, true, "管理员手工解锁");
    }
}
