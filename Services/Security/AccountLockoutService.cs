#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ApexHMI.Interfaces;
using Microsoft.Data.Sqlite;
using Serilog;

namespace ApexHMI.Services.Security;

/// <summary>
/// M6.1: 账户锁定服务 — 锁定状态唯一来源（M5.2 临时把 LockedUntil 内联在 UserAccount 上，
/// 本次完成迁移）。
/// <para>跟踪每用户连续登录失败次数；超阈值则记录锁定时间戳；登录前查询即可。</para>
/// <para>M7.1: 持久化到 <c>data/audit.db</c> 的 <c>account_lockout</c> 表，
/// 修复 M6.1 仅内存导致"进程重启锁定丢失"的隐患。
/// 启动时加载有效记录 + 清过期，任何 mutation 同步表+内存。</para>
/// </summary>
public sealed class AccountLockoutService
{
    private readonly IAuditService _audit;
    private readonly string? _dbPath;
    private readonly object _lock = new();
    private readonly Dictionary<string, int> _failsByUser = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTime> _lockedUntil = new(StringComparer.OrdinalIgnoreCase);
    private bool _dbInited;

    /// <summary>连续失败几次后锁定（默认 5）。</summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>锁定持续时间（默认 15 分钟）。</summary>
    public TimeSpan LockoutDuration { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>无 SQLite 路径的构造（旧测试 / 内存模式）。</summary>
    public AccountLockoutService(IAuditService audit)
        : this(audit, dataDirectory: null)
    {
    }

    /// <summary>
    /// M7.1: 注入数据目录后开启 SQLite 持久化。
    /// 表 <c>account_lockout(username PK, failure_count, locked_until_unix_ms, last_failure_at_unix_ms)</c>。
    /// </summary>
    public AccountLockoutService(IAuditService audit, string? dataDirectory)
    {
        _audit = audit;
        if (!string.IsNullOrEmpty(dataDirectory))
        {
            try
            {
                Directory.CreateDirectory(dataDirectory);
                _dbPath = Path.Combine(dataDirectory, "audit.db");
                LoadFromDb();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "AccountLockoutService: SQLite 初始化失败，退化为内存模式");
                _dbPath = null;
            }
        }
    }

    /// <summary>登录前调用：true 表示账号当前处于锁定，应直接拒绝。</summary>
    public bool IsLocked(string username)
    {
        if (string.IsNullOrEmpty(username)) return false;
        bool expired = false;
        lock (_lock)
        {
            if (_lockedUntil.TryGetValue(username, out var until))
            {
                if (until > DateTime.UtcNow) return true;
                _lockedUntil.Remove(username);
                expired = true;
            }
        }
        if (expired) DbDelete(username);
        return false;
    }

    /// <summary>查询某用户的锁定到期时间（null = 未锁定）。返回 UTC 时间。</summary>
    public DateTime? GetLockedUntil(string username)
    {
        if (string.IsNullOrEmpty(username)) return null;
        bool expired = false;
        DateTime? result = null;
        lock (_lock)
        {
            if (_lockedUntil.TryGetValue(username, out var until))
            {
                if (until > DateTime.UtcNow) result = until;
                else { _lockedUntil.Remove(username); expired = true; }
            }
        }
        if (expired) DbDelete(username);
        return result;
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
        DateTime nowUtc = DateTime.UtcNow;
        DateTime? lockedUntil = null;
        lock (_lock)
        {
            _failsByUser.TryGetValue(username, out var prior);
            n = prior + 1;
            _failsByUser[username] = n;
            if (n >= MaxAttempts)
            {
                lockedUntil = nowUtc + LockoutDuration;
                _lockedUntil[username] = lockedUntil.Value;
                _failsByUser[username] = 0;
                justLocked = true;
            }
        }
        DbUpsert(username, justLocked ? 0 : n, lockedUntil, nowUtc);
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
        DbDelete(username);
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
        DbDelete(username);
        Log.Information("AccountLockout: 管理员 {Op} 手工解锁 {U}", operatorUser, username);
        return _audit.LogOperationAsync(operatorUser, "account-unlock", username, true, "管理员手工解锁");
    }

    // ---------- SQLite 持久化 ----------

    private void EnsureDb()
    {
        if (_dbInited || _dbPath is null) return;
        using var conn = OpenConn();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS account_lockout (
                username              TEXT    PRIMARY KEY COLLATE NOCASE,
                failure_count         INTEGER NOT NULL DEFAULT 0,
                locked_until_unix_ms  INTEGER,
                last_failure_unix_ms  INTEGER NOT NULL
            );
        ";
        cmd.ExecuteNonQuery();
        _dbInited = true;
    }

    private SqliteConnection OpenConn()
    {
        var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        return conn;
    }

    private void LoadFromDb()
    {
        if (_dbPath is null) return;
        EnsureDb();
        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var expired = new List<string>();
        using var conn = OpenConn();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT username, failure_count, locked_until_unix_ms FROM account_lockout";
            using var rdr = cmd.ExecuteReader();
            lock (_lock)
            {
                while (rdr.Read())
                {
                    var user = rdr.GetString(0);
                    var fails = rdr.GetInt32(1);
                    if (!rdr.IsDBNull(2))
                    {
                        var untilMs = rdr.GetInt64(2);
                        if (untilMs > nowMs)
                        {
                            _lockedUntil[user] = DateTimeOffset.FromUnixTimeMilliseconds(untilMs).UtcDateTime;
                        }
                        else
                        {
                            expired.Add(user);
                            continue;
                        }
                    }
                    if (fails > 0) _failsByUser[user] = fails;
                }
            }
        }
        if (expired.Count > 0)
        {
            using var del = conn.CreateCommand();
            del.CommandText = "DELETE FROM account_lockout WHERE username = $u";
            var p = del.CreateParameter(); p.ParameterName = "$u"; del.Parameters.Add(p);
            foreach (var u in expired) { p.Value = u; del.ExecuteNonQuery(); }
        }
        Log.Information("AccountLockoutService: SQLite 加载完成 locked={L} fails={F} expired-cleared={E}",
            _lockedUntil.Count, _failsByUser.Count, expired.Count);
    }

    private void DbUpsert(string username, int failureCount, DateTime? lockedUntilUtc, DateTime lastFailureUtc)
    {
        if (_dbPath is null) return;
        try
        {
            EnsureDb();
            using var conn = OpenConn();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO account_lockout (username, failure_count, locked_until_unix_ms, last_failure_unix_ms)
                VALUES ($u, $f, $lu, $lf)
                ON CONFLICT(username) DO UPDATE SET
                    failure_count = excluded.failure_count,
                    locked_until_unix_ms = excluded.locked_until_unix_ms,
                    last_failure_unix_ms = excluded.last_failure_unix_ms;";
            cmd.Parameters.AddWithValue("$u", username);
            cmd.Parameters.AddWithValue("$f", failureCount);
            cmd.Parameters.AddWithValue("$lu",
                lockedUntilUtc.HasValue
                    ? (object)new DateTimeOffset(DateTime.SpecifyKind(lockedUntilUtc.Value, DateTimeKind.Utc)).ToUnixTimeMilliseconds()
                    : DBNull.Value);
            cmd.Parameters.AddWithValue("$lf",
                new DateTimeOffset(DateTime.SpecifyKind(lastFailureUtc, DateTimeKind.Utc)).ToUnixTimeMilliseconds());
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "AccountLockoutService: DbUpsert 失败 user={U}", username);
        }
    }

    private void DbDelete(string username)
    {
        if (_dbPath is null) return;
        try
        {
            EnsureDb();
            using var conn = OpenConn();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM account_lockout WHERE username = $u";
            cmd.Parameters.AddWithValue("$u", username);
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "AccountLockoutService: DbDelete 失败 user={U}", username);
        }
    }
}
