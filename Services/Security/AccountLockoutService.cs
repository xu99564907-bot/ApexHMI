#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ApexHMI.Interfaces;
using Serilog;

namespace ApexHMI.Services.Security;

/// <summary>
/// M6.1: 账户锁定服务 — 锁定状态唯一来源（M5.2 临时把 LockedUntil 内联在 UserAccount 上，
/// 本次完成迁移）。
/// <para>跟踪每用户连续登录失败次数；超阈值则记录锁定时间戳到内存；登录前查询即可。</para>
/// <para>UserService 不再保存 LockedUntil 字段，只在 Authenticate 时调本服务的三个 API：
/// <see cref="IsLocked"/> / <see cref="RegisterFailure"/> / <see cref="ResetCounter"/>。</para>
/// </summary>
public sealed class AccountLockoutService
{
    private readonly IAuditService _audit;
    private readonly object _lock = new();
    private readonly Dictionary<string, int> _failsByUser = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTime> _lockedUntil = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>连续失败几次后锁定（默认 5）。</summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>锁定持续时间（默认 15 分钟）。</summary>
    public TimeSpan LockoutDuration { get; set; } = TimeSpan.FromMinutes(15);

    public AccountLockoutService(IAuditService audit)
    {
        _audit = audit;
    }

    /// <summary>登录前调用：true 表示账号当前处于锁定，应直接拒绝。</summary>
    public bool IsLocked(string username)
    {
        if (string.IsNullOrEmpty(username)) return false;
        lock (_lock)
        {
            if (_lockedUntil.TryGetValue(username, out var until))
            {
                if (until > DateTime.Now) return true;
                // 已自然解锁 — 清掉过期记录
                _lockedUntil.Remove(username);
            }
            return false;
        }
    }

    /// <summary>查询某用户的锁定到期时间（null = 未锁定）。</summary>
    public DateTime? GetLockedUntil(string username)
    {
        if (string.IsNullOrEmpty(username)) return null;
        lock (_lock)
        {
            if (_lockedUntil.TryGetValue(username, out var until))
            {
                if (until > DateTime.Now) return until;
                _lockedUntil.Remove(username);
            }
            return null;
        }
    }

    /// <summary>查询当前连续失败次数。</summary>
    public int GetFailureCount(string username)
    {
        if (string.IsNullOrEmpty(username)) return 0;
        lock (_lock) return _failsByUser.TryGetValue(username, out var n) ? n : 0;
    }

    /// <summary>登录失败后调用。返回 true 表示本次失败已触发锁定。</summary>
    public bool RegisterFailure(string username)
    {
        if (string.IsNullOrEmpty(username)) return false;
        int n;
        bool justLocked = false;
        lock (_lock)
        {
            _failsByUser.TryGetValue(username, out var prior);
            n = prior + 1;
            _failsByUser[username] = n;
            if (n >= MaxAttempts)
            {
                _lockedUntil[username] = DateTime.Now + LockoutDuration;
                _failsByUser[username] = 0;
                justLocked = true;
            }
        }
        if (justLocked)
        {
            _ = _audit.LogOperationAsync(username, "account-locked", username, false,
                $"连续失败 {n} 次，锁定 {LockoutDuration.TotalMinutes:F0} 分钟");
            Log.Warning("AccountLockout: 用户 {User} 因连续失败 {N} 次被锁定", username, n);
        }
        return justLocked;
    }

    /// <summary>登录成功后调用：清空累计计数 + 解锁。</summary>
    public void ResetCounter(string username)
    {
        if (string.IsNullOrEmpty(username)) return;
        lock (_lock)
        {
            _failsByUser.Remove(username);
            _lockedUntil.Remove(username);
        }
    }

    /// <summary>兼容旧名：等价 <see cref="ResetCounter"/>。</summary>
    public void RegisterSuccess(string username) => ResetCounter(username);

    /// <summary>管理员手工解锁。</summary>
    public Task UnlockAsync(string username, string operatorUser)
    {
        lock (_lock)
        {
            _failsByUser.Remove(username);
            _lockedUntil.Remove(username);
        }
        Log.Information("AccountLockout: 管理员 {Op} 手工解锁 {U}", operatorUser, username);
        return _audit.LogOperationAsync(operatorUser, "account-unlock", username, true, "管理员手工解锁");
    }
}
