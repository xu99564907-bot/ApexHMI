#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ApexHMI.Interfaces;
using Microsoft.Data.Sqlite;
using Serilog;

namespace ApexHMI.Services;

/// <summary>
/// M4.3: SQLite 后端审计服务，替代 M3.2 的 CSV 实现。
/// <list type="bullet">
///   <item>表 audit_log (id PK AUTOINCREMENT, timestamp INTEGER UnixMs, user, action, target, success INTEGER 0/1, detail)</item>
///   <item>索引 (timestamp), (user), (target) — 查询友好</item>
///   <item>LogOperationAsync 同步插入（轻量 INSERT，PLC 写操作链已是 async）</item>
///   <item>QueryAsync(from, to, user?, action?) 范围查询</item>
///   <item>每次写入触发轻量清理（节流 60s 一次），保留最近 90 天</item>
///   <item>同时维持 M3.2 的 _memorySink（让 UI 审计 Tab 立即看到一条）</item>
/// </list>
/// CSV 实现保留为 fallback（开发/导出场景）。
/// </summary>
public sealed class AuditServiceSqlite : IAuditService
{
    private static readonly TimeSpan RetentionWindow = TimeSpan.FromDays(90);

    private readonly string _dbPath;
    private readonly object _dbLock = new();
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private Action<string, string, string, string>? _memorySink;
    private bool _dbInited;
    private long _lastCleanupTicks;

    public AuditServiceSqlite(string dataDirectory, Action<string, string, string, string>? memorySink = null)
    {
        Directory.CreateDirectory(dataDirectory);
        _dbPath = Path.Combine(dataDirectory, "audit.db");
        _memorySink = memorySink;
    }

    /// <summary>M3.2 兼容：运行时注入内存 sink（MainWindowViewModel 构造后调用）。</summary>
    public void AttachMemorySink(Action<string, string, string, string> sink) => _memorySink = sink;

    public async Task LogOperationAsync(string user, string action, string target, bool success, string? detail = null)
    {
        // 1) 内存 sink（同步派发到 UI）
        try { _memorySink?.Invoke(action, target, success ? "成功" : "失败", detail ?? string.Empty); }
        catch (Exception ex) { Log.Debug(ex, "AuditServiceSqlite: 内存 sink 写入失败"); }

        // 2) SQLite
        await _writeGate.WaitAsync().ConfigureAwait(false);
        try
        {
            EnsureDb();
            // M7.3: 持久化时间戳统一 UTC（Unix ms 本身与时区无关，但显式以 UTC 入口避免歧义）
            var tsMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            lock (_dbLock)
            {
                using var conn = OpenConn();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"INSERT INTO audit_log (timestamp, user, action, target, success, detail)
                                    VALUES ($ts, $u, $a, $t, $s, $d)";
                cmd.Parameters.AddWithValue("$ts", tsMs);
                cmd.Parameters.AddWithValue("$u",  user ?? string.Empty);
                cmd.Parameters.AddWithValue("$a",  action ?? string.Empty);
                cmd.Parameters.AddWithValue("$t",  target ?? string.Empty);
                cmd.Parameters.AddWithValue("$s",  success ? 1 : 0);
                cmd.Parameters.AddWithValue("$d",  (object?)detail ?? string.Empty);
                cmd.ExecuteNonQuery();
            }
            MaybeCleanup();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "AuditServiceSqlite: 写入失败 path={Path}", _dbPath);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    /// <summary>按时间范围 + 可选 user/action 过滤查询。</summary>
    public Task<IReadOnlyList<AuditRecord>> QueryAsync(DateTime from, DateTime to, string? user = null, string? action = null)
    {
        EnsureDb();
        // M7.3: 查询入参可能是 Local 或 Utc；按 Kind 自动归一化到 UTC Unix ms
        var fromMs = ToUnixMs(from);
        var toMs   = ToUnixMs(to);
        var list = new List<AuditRecord>();
        lock (_dbLock)
        {
            try
            {
                using var conn = OpenConn();
                using var cmd = conn.CreateCommand();
                var sql = "SELECT id, timestamp, user, action, target, success, detail FROM audit_log " +
                          "WHERE timestamp BETWEEN $a AND $b";
                if (!string.IsNullOrWhiteSpace(user))   sql += " AND user = $u";
                if (!string.IsNullOrWhiteSpace(action)) sql += " AND action = $act";
                sql += " ORDER BY timestamp ASC";
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("$a", fromMs);
                cmd.Parameters.AddWithValue("$b", toMs);
                if (!string.IsNullOrWhiteSpace(user))   cmd.Parameters.AddWithValue("$u", user);
                if (!string.IsNullOrWhiteSpace(action)) cmd.Parameters.AddWithValue("$act", action);

                using var rdr = cmd.ExecuteReader();
                while (rdr.Read())
                {
                    list.Add(new AuditRecord(
                        rdr.GetInt64(0),
                        // M7.3: 读出按 LocalTime 显示（DB 内部存的是 UTC Unix ms）
                        DateTimeOffset.FromUnixTimeMilliseconds(rdr.GetInt64(1)).LocalDateTime,
                        rdr.GetString(2),
                        rdr.GetString(3),
                        rdr.GetString(4),
                        rdr.GetInt64(5) == 1,
                        rdr.IsDBNull(6) ? string.Empty : rdr.GetString(6)));
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "AuditServiceSqlite: 查询失败");
            }
        }
        return Task.FromResult<IReadOnlyList<AuditRecord>>(list);
    }

    private void EnsureDb()
    {
        if (_dbInited) return;
        lock (_dbLock)
        {
            if (_dbInited) return;
            var dir = Path.GetDirectoryName(_dbPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            using var conn = OpenConn();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS audit_log (
                    id        INTEGER PRIMARY KEY AUTOINCREMENT,
                    timestamp INTEGER NOT NULL,
                    user      TEXT    NOT NULL,
                    action    TEXT    NOT NULL,
                    target    TEXT    NOT NULL,
                    success   INTEGER NOT NULL,
                    detail    TEXT
                );
                CREATE INDEX IF NOT EXISTS idx_audit_ts     ON audit_log(timestamp);
                CREATE INDEX IF NOT EXISTS idx_audit_user   ON audit_log(user);
                CREATE INDEX IF NOT EXISTS idx_audit_target ON audit_log(target);
            ";
            cmd.ExecuteNonQuery();
            _dbInited = true;
            Log.Information("AuditServiceSqlite: SQLite 已初始化 path={Path}", _dbPath);
        }
    }

    /// <summary>M7.3: 把 DateTime（Local / Utc / Unspecified）统一归一化到 UTC Unix ms。</summary>
    private static long ToUnixMs(DateTime t)
    {
        var utc = t.Kind switch
        {
            DateTimeKind.Utc => t,
            DateTimeKind.Local => t.ToUniversalTime(),
            _ => DateTime.SpecifyKind(t, DateTimeKind.Local).ToUniversalTime(),
        };
        return new DateTimeOffset(utc, TimeSpan.Zero).ToUnixTimeMilliseconds();
    }

    private SqliteConnection OpenConn()
    {
        var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        return conn;
    }

    private void MaybeCleanup()
    {
        var now = Environment.TickCount;
        if (now - Interlocked.Read(ref _lastCleanupTicks) < 60_000) return;
        Interlocked.Exchange(ref _lastCleanupTicks, now);
        // M7.3: 清理截止点统一 UTC
        var cutoff = DateTimeOffset.UtcNow.Subtract(RetentionWindow).ToUnixTimeMilliseconds();
        lock (_dbLock)
        {
            try
            {
                using var conn = OpenConn();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "DELETE FROM audit_log WHERE timestamp < $c";
                cmd.Parameters.AddWithValue("$c", cutoff);
                var n = cmd.ExecuteNonQuery();
                if (n > 0) Log.Information("AuditServiceSqlite: 清理 {N} 条 >90d 过期记录", n);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "AuditServiceSqlite: 清理失败");
            }
        }
    }
}

/// <summary>M4.3 审计记录查询结果。</summary>
public readonly record struct AuditRecord(
    long Id, DateTime Timestamp, string User, string Action, string Target, bool Success, string Detail);
