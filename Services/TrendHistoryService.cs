using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ApexHMI.Interfaces;
using ApexHMI.Models;
using Microsoft.Data.Sqlite;
using Serilog;

namespace ApexHMI.Services;

/// <summary>
/// 趋势历史服务。
/// <para>原 CSV 接口保留向后兼容（AppendAsync/LoadAsync），同时 P10H 新增 SQLite 后端：</para>
/// <list type="bullet">
///   <item><see cref="EnableLogging"/> / <see cref="DisableLogging"/>：登记需归档的 tag + 周期</item>
///   <item><see cref="LogValue"/>：由订阅回调写入一条 sample</item>
///   <item><see cref="Query"/>：按时间范围读取历史</item>
///   <item>自动滚动：每次 LogValue 触发一次轻量清理，保留最近 7 天</item>
/// </list>
/// </summary>
public class TrendHistoryService : ITrendHistoryService
{
    // ========== 旧 CSV API（向后兼容）==========

    public async Task AppendAsync(string path, IEnumerable<TrendSample> samples)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
        var exists = File.Exists(path);
        using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        using var writer = new StreamWriter(stream, Encoding.UTF8);
        if (!exists)
        {
            await writer.WriteLineAsync("Time,Category,Value,Source");
        }
        foreach (var s in samples)
        {
            await writer.WriteLineAsync($"{s.Time:yyyy-MM-dd HH:mm:ss},{s.Category},{s.Value:F3},{s.Source}");
        }
    }

    public async Task<List<TrendSample>> LoadAsync(string path)
    {
        var result = new List<TrendSample>();
        if (!File.Exists(path)) return result;
        var lines = await Compat.ReadAllLinesAsync(path, Encoding.UTF8);
        foreach (var line in lines.Skip(1))
        {
            var parts = line.Split(',');
            if (parts.Length < 4) continue;
            result.Add(new TrendSample
            {
                Time = DateTime.TryParse(parts[0], out var t) ? t : DateTime.Now,
                Category = parts[1],
                Value = double.TryParse(parts[2], out var v) ? v : 0,
                Source = parts[3]
            });
        }
        return result;
    }

    // ========== P10H SQLite 后端 ==========

    private static readonly string DbPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "data", "trend_history.db");
    private static readonly TimeSpan RetentionWindow = TimeSpan.FromDays(7);
    private readonly object _dbLock = new();
    private readonly ConcurrentDictionary<string, int> _enabledTags = new(StringComparer.OrdinalIgnoreCase);
    private long _lastCleanupMs;
    private bool _dbInited;

    private void EnsureDb()
    {
        if (_dbInited) return;
        lock (_dbLock)
        {
            if (_dbInited) return;
            var dir = Path.GetDirectoryName(DbPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            using var conn = OpenConn();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS tag_history (
                    tag  TEXT NOT NULL,
                    ts   INTEGER NOT NULL,
                    value REAL NOT NULL
                );
                CREATE INDEX IF NOT EXISTS idx_tag_ts ON tag_history(tag, ts);
            ";
            cmd.ExecuteNonQuery();
            _dbInited = true;
            Log.Information("TrendHistory: SQLite 已初始化 path={Path}", DbPath);
        }
    }

    private static SqliteConnection OpenConn()
    {
        var conn = new SqliteConnection($"Data Source={DbPath}");
        conn.Open();
        return conn;
    }

    /// <summary>登记 tag 为归档对象（intervalMs 仅作记录，实际写入由 LogValue 触发）。</summary>
    public void EnableLogging(string tagId, int intervalMs = 1000)
    {
        if (string.IsNullOrWhiteSpace(tagId)) return;
        EnsureDb();
        _enabledTags[tagId] = Math.Max(100, intervalMs);
        Log.Information("TrendHistory: 启用归档 tag={Tag} interval={Interval}ms", tagId, intervalMs);
    }

    public void DisableLogging(string tagId)
    {
        if (string.IsNullOrWhiteSpace(tagId)) return;
        _enabledTags.TryRemove(tagId, out _);
    }

    /// <summary>记录一条值（被订阅回调调用）；如果该 tag 未 EnableLogging，则忽略。</summary>
    public void LogValue(string tagId, double value, DateTime? timestamp = null)
    {
        if (!_enabledTags.ContainsKey(tagId)) return;
        EnsureDb();
        var ts = new DateTimeOffset(timestamp ?? DateTime.UtcNow).ToUnixTimeMilliseconds();
        lock (_dbLock)
        {
            try
            {
                using var conn = OpenConn();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "INSERT INTO tag_history (tag, ts, value) VALUES ($tag, $ts, $v)";
                cmd.Parameters.AddWithValue("$tag", tagId);
                cmd.Parameters.AddWithValue("$ts", ts);
                cmd.Parameters.AddWithValue("$v", value);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "TrendHistory: 写入失败 tag={Tag}", tagId);
            }
        }
        MaybeCleanup();
    }

    /// <summary>查询某 tag 在 [fromTs,toTs] 范围内的样本，按时间升序。</summary>
    public IReadOnlyList<TrendHistoryPoint> Query(string tagId, DateTime fromTs, DateTime toTs)
    {
        EnsureDb();
        var fromMs = new DateTimeOffset(fromTs.ToUniversalTime()).ToUnixTimeMilliseconds();
        var toMs = new DateTimeOffset(toTs.ToUniversalTime()).ToUnixTimeMilliseconds();
        var result = new List<TrendHistoryPoint>();
        lock (_dbLock)
        {
            try
            {
                using var conn = OpenConn();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT ts, value FROM tag_history WHERE tag=$tag AND ts BETWEEN $a AND $b ORDER BY ts ASC";
                cmd.Parameters.AddWithValue("$tag", tagId);
                cmd.Parameters.AddWithValue("$a", fromMs);
                cmd.Parameters.AddWithValue("$b", toMs);
                using var rdr = cmd.ExecuteReader();
                while (rdr.Read())
                {
                    var ts = rdr.GetInt64(0);
                    var v = rdr.GetDouble(1);
                    result.Add(new TrendHistoryPoint(DateTimeOffset.FromUnixTimeMilliseconds(ts).LocalDateTime, v));
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "TrendHistory: 查询失败 tag={Tag}", tagId);
            }
        }
        return result;
    }

    /// <summary>
    /// M7.5: 按时间桶聚合查询 — 用于 TrendView 长跨度 LOD 降采样。
    /// 每 <paramref name="bucketMs"/> 毫秒一个桶，输出 AVG(value)。
    /// 例 bucketMs = 60_000 → 每分钟一点，bucketMs = 900_000 → 每 15 分钟一点。
    /// </summary>
    /// <param name="tagId">tag 名</param>
    /// <param name="fromTs">起始（Local 或 Utc 均可）</param>
    /// <param name="toTs">终止</param>
    /// <param name="bucketMs">桶宽（毫秒）；&lt;=0 视为 1ms（等价 Query）</param>
    public IReadOnlyList<TrendHistoryPoint> QueryAggregated(string tagId, DateTime fromTs, DateTime toTs, long bucketMs)
    {
        if (bucketMs <= 0) return Query(tagId, fromTs, toTs);
        EnsureDb();
        var fromMs = new DateTimeOffset(fromTs.ToUniversalTime()).ToUnixTimeMilliseconds();
        var toMs   = new DateTimeOffset(toTs.ToUniversalTime()).ToUnixTimeMilliseconds();
        var result = new List<TrendHistoryPoint>();
        lock (_dbLock)
        {
            try
            {
                using var conn = OpenConn();
                using var cmd = conn.CreateCommand();
                // 桶 key = ts / bucketMs；输出每桶 AVG(value)、桶起点 ts；按时间升序
                cmd.CommandText = @"
                    SELECT (ts / $bucket) * $bucket AS bucket_ts, AVG(value) AS v
                    FROM tag_history
                    WHERE tag = $tag AND ts BETWEEN $a AND $b
                    GROUP BY bucket_ts
                    ORDER BY bucket_ts ASC";
                cmd.Parameters.AddWithValue("$bucket", bucketMs);
                cmd.Parameters.AddWithValue("$tag", tagId);
                cmd.Parameters.AddWithValue("$a", fromMs);
                cmd.Parameters.AddWithValue("$b", toMs);
                using var rdr = cmd.ExecuteReader();
                while (rdr.Read())
                {
                    var ts = rdr.GetInt64(0);
                    var v = rdr.GetDouble(1);
                    result.Add(new TrendHistoryPoint(DateTimeOffset.FromUnixTimeMilliseconds(ts).LocalDateTime, v));
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "TrendHistory: 聚合查询失败 tag={Tag} bucket={Bucket}", tagId, bucketMs);
            }
        }
        return result;
    }

    /// <summary>每 60s 触发一次清理：删除 7 天前数据。</summary>
    private void MaybeCleanup()
    {
        var now = Environment.TickCount;
        if (now - Interlocked.Read(ref _lastCleanupMs) < 60_000) return;
        Interlocked.Exchange(ref _lastCleanupMs, now);
        var cutoff = new DateTimeOffset(DateTime.UtcNow.Subtract(RetentionWindow)).ToUnixTimeMilliseconds();
        lock (_dbLock)
        {
            try
            {
                using var conn = OpenConn();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "DELETE FROM tag_history WHERE ts < $cutoff";
                cmd.Parameters.AddWithValue("$cutoff", cutoff);
                var n = cmd.ExecuteNonQuery();
                if (n > 0) Log.Information("TrendHistory: 清理 {N} 条过期记录", n);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "TrendHistory: 清理失败");
            }
        }
    }
}

/// <summary>P10H 历史点（时间 + 数值）。</summary>
public readonly record struct TrendHistoryPoint(DateTime Time, double Value);
