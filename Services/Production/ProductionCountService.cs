using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using ApexHMI.Interfaces;
using ApexHMI.Models;
using ApexHMI.Models.Production;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using Serilog;

namespace ApexHMI.Services.Production;

/// <summary>
/// 生产计数服务实现。订阅 PLC 的 OK.Total / NG.Total 累计计数，差值法落盘到 SQLite，
/// 提供分时 / 班次 / 历史查询。详见 docs/count-module-design.md。
/// </summary>
public sealed class ProductionCountService : IProductionCountService, IDisposable
{
    private const string TagNameOk = "OK.Total";
    private const string TagNameNg = "NG.Total";

    /// <summary>
    /// 初始化 SQLitePCL 原生库。.NET Framework 4.8 不会自动加载 e_sqlite3.dll，
    /// 必须在使用 SqliteConnection 前显式 Init 一次。
    /// </summary>
    static ProductionCountService()
    {
        try { SQLitePCL.Batteries_V2.Init(); }
        catch (Exception ex) { Log.Error(ex, "SQLitePCL.Batteries_V2.Init 失败"); }
    }

    private readonly IOpcUaService _opcUa;
    private readonly ShiftOptions _shift;
    private readonly string _dbPath;
    private readonly string _connectionString;

    /// <summary>每个 source 上次见到的累计值，差值法基线。</summary>
    private readonly Dictionary<string, long?> _lastCounter = new()
    {
        { "OK", null },
        { "NG", null },
    };

    private readonly object _lock = new();
    private bool _attached;
    private bool _disposed;

    public ProductionCountService(IOpcUaService opcUa, IOptions<AppOptions> options)
    {
        _opcUa = opcUa;
        var opts = options?.Value ?? new AppOptions();
        _shift = opts.Shift;
        var configDir = Path.Combine(AppContext.BaseDirectory, opts.ConfigFiles?.ConfigDirectoryName ?? "config");
        Directory.CreateDirectory(configDir);
        _dbPath = Path.Combine(configDir, _shift.DatabaseFileName);
        _connectionString = $"Data Source={_dbPath};Cache=Shared";
        InitializeSchema();

        // 构造时自动订阅 OpcUa 事件；事件订阅本身不需要 OPC UA 已连接，
        // 实际数据流入会在 PLC 推送 OK.Total / NG.Total 时触发回调。
        _opcUa.TagValueChanged += HandleTagValueChanged;
        _attached = true;
    }

    public event Action<string>? EventInserted;

    // ============================================================
    // schema 初始化
    // ============================================================

    private void InitializeSchema()
    {
        try
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();

            // 启用 WAL 模式：写抖动小、读不阻塞写
            using (var pragma = conn.CreateCommand())
            {
                pragma.CommandText = "PRAGMA journal_mode=WAL;";
                pragma.ExecuteNonQuery();
            }

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS production_event (
    id            INTEGER PRIMARY KEY AUTOINCREMENT,
    ts_utc        TEXT    NOT NULL,
    source        TEXT    NOT NULL,
    delta         INTEGER NOT NULL,
    counter_value INTEGER NOT NULL,
    note          TEXT    NULL
);
CREATE INDEX IF NOT EXISTS idx_event_ts_source ON production_event(source, ts_utc);
CREATE INDEX IF NOT EXISTS idx_event_ts ON production_event(ts_utc);
";
            cmd.ExecuteNonQuery();

            Log.Information("ProductionCountService: SQLite schema 已就绪 path={Path}", _dbPath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ProductionCountService: 初始化 SQLite schema 失败 path={Path}", _dbPath);
        }
    }

    // ============================================================
    // OPC UA 订阅 / 差值法入账
    // ============================================================

    public Task AttachAsync()
    {
        lock (_lock)
        {
            if (_attached) return Task.CompletedTask;
            _opcUa.TagValueChanged += HandleTagValueChanged;
            _attached = true;
        }
        Log.Information("ProductionCountService: 已附加到 OpcUaService.TagValueChanged");
        return Task.CompletedTask;
    }

    private void HandleTagValueChanged(string tagName, string value)
    {
        // 仅关心 OK.Total / NG.Total，按 tagName 末段匹配
        if (string.IsNullOrEmpty(tagName)) return;
        if (tagName.EndsWith(TagNameOk, StringComparison.OrdinalIgnoreCase))
            OnTagValueChanged("OK", value);
        else if (tagName.EndsWith(TagNameNg, StringComparison.OrdinalIgnoreCase))
            OnTagValueChanged("NG", value);
    }

    public void OnTagValueChanged(string source, string? rawValue)
    {
        if (string.IsNullOrEmpty(rawValue)) return;
        if (!long.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var current))
            return;
        if (source != "OK" && source != "NG") return;

        long delta;
        string? note = null;
        long counterToStore = current;

        lock (_lock)
        {
            var prev = _lastCounter[source];
            _lastCounter[source] = current;

            if (prev is null)
            {
                // 第一次读取：仅记基线，不写事件
                Log.Debug("ProductionCountService: 基线 source={Source} counter={Counter}", source, current);
                return;
            }

            if (current >= prev.Value)
            {
                delta = current - prev.Value;
                if (delta == 0) return;
            }
            else
            {
                // PLC 重启或 UDINT 溢出：把 current 当新基线，本次只入账 current 个
                delta = current;
                note = "reset";
                Log.Warning("ProductionCountService: 检测到 reset source={Source} prev={Prev} current={Current}",
                    source, prev.Value, current);
            }
        }

        if (delta <= 0) return;

        try
        {
            InsertEvent(DateTime.UtcNow, source, (int)Math.Min(delta, int.MaxValue), counterToStore, note);
            EventInserted?.Invoke(source);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ProductionCountService: 写入计数事件失败 source={Source} delta={Delta}", source, delta);
        }
    }

    private void InsertEvent(DateTime tsUtc, string source, int delta, long counterValue, string? note)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
INSERT INTO production_event (ts_utc, source, delta, counter_value, note)
VALUES (@ts, @src, @delta, @cv, @note);";
        cmd.Parameters.AddWithValue("@ts", tsUtc.ToString("o", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("@src", source);
        cmd.Parameters.AddWithValue("@delta", delta);
        cmd.Parameters.AddWithValue("@cv", counterValue);
        cmd.Parameters.AddWithValue("@note", (object?)note ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    // ============================================================
    // 查询 API
    // ============================================================

    /// <summary>把 source = "Total" 拆成 OK + NG。其它直接走单 source 查询。</summary>
    private static IEnumerable<string> ResolveSources(string source) =>
        source switch
        {
            "Total" => new[] { "OK", "NG" },
            "OK" => new[] { "OK" },
            "NG" => new[] { "NG" },
            _ => Array.Empty<string>(),
        };

    public IReadOnlyList<HourBucket> GetHourlyToday(string source)
    {
        var sources = ResolveSources(source);
        var result = new List<HourBucket>(24);
        var shiftStartLocal = ResolveTodayShiftStartLocal();
        var shiftStartUtc = shiftStartLocal.ToUniversalTime();

        // 24 桶累加器
        var counts = new int[24];
        try
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            foreach (var src in sources)
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
SELECT
    CAST((julianday(ts_utc) - julianday(@start)) * 24 AS INTEGER) AS hour_idx,
    SUM(delta) AS cnt
FROM production_event
WHERE source = @src
  AND ts_utc >= @start
  AND ts_utc < @end
GROUP BY hour_idx;";
                cmd.Parameters.AddWithValue("@start", shiftStartUtc.ToString("o", CultureInfo.InvariantCulture));
                cmd.Parameters.AddWithValue("@end", shiftStartUtc.AddHours(24).ToString("o", CultureInfo.InvariantCulture));
                cmd.Parameters.AddWithValue("@src", src);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var idx = reader.GetInt32(0);
                    var cnt = reader.GetInt32(1);
                    if (idx >= 0 && idx < 24) counts[idx] += cnt;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ProductionCountService: GetHourlyToday 查询失败 source={Source}", source);
        }

        for (var i = 0; i < 24; i++)
        {
            var bucketStart = shiftStartLocal.AddHours(i);
            var bucketEnd = shiftStartLocal.AddHours(i + 1);
            result.Add(new HourBucket(
                Index: i,
                TimeRangeText: $"{bucketStart:HH:mm}-{bucketEnd:HH:mm}",
                BucketStartLocal: bucketStart,
                Count: counts[i]));
        }
        return result;
    }

    public ShiftTotals GetShiftTotals(string source)
    {
        var hourly = GetHourlyToday(source);
        // 桶 0..N 是白班，N+1..23 是夜班；N = 夜班开始 - 白班开始（小时数）
        var nightOffset = ResolveNightShiftHourOffset();
        var day = 0;
        var night = 0;
        for (var i = 0; i < hourly.Count; i++)
        {
            if (i < nightOffset) day += hourly[i].Count;
            else night += hourly[i].Count;
        }
        return new ShiftTotals(day, night);
    }

    public IReadOnlyList<DailyTotal> GetDailyHistory(string source, int days)
    {
        if (days <= 0) return Array.Empty<DailyTotal>();
        var sources = ResolveSources(source);
        var result = new List<DailyTotal>(days);
        var endDate = DateTime.Now.Date.AddDays(1);
        var startDate = endDate.AddDays(-days);

        var dayCounts = new Dictionary<DateTime, int>(days);
        for (var d = startDate; d < endDate; d = d.AddDays(1))
            dayCounts[d] = 0;

        try
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            foreach (var src in sources)
            {
                using var cmd = conn.CreateCommand();
                // SQLite date(...) 返回 'YYYY-MM-DD'，按本地时区聚合
                cmd.CommandText = @"
SELECT date(ts_utc, 'localtime') AS d, SUM(delta) AS cnt
FROM production_event
WHERE source = @src
  AND ts_utc >= @start
  AND ts_utc < @end
GROUP BY d;";
                cmd.Parameters.AddWithValue("@src", src);
                cmd.Parameters.AddWithValue("@start", startDate.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture));
                cmd.Parameters.AddWithValue("@end", endDate.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture));
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var dStr = reader.GetString(0);
                    if (DateTime.TryParseExact(dStr, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                    {
                        if (dayCounts.ContainsKey(d)) dayCounts[d] += reader.GetInt32(1);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ProductionCountService: GetDailyHistory 查询失败 source={Source} days={Days}", source, days);
        }

        for (var d = startDate; d < endDate; d = d.AddDays(1))
            result.Add(new DailyTotal(d, dayCounts[d]));
        return result;
    }

    public int GetTotalInRange(string source, DateTime fromUtc, DateTime toUtc)
    {
        var sources = ResolveSources(source);
        var total = 0;
        try
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            foreach (var src in sources)
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
SELECT COALESCE(SUM(delta), 0)
FROM production_event
WHERE source = @src AND ts_utc >= @from AND ts_utc < @to;";
                cmd.Parameters.AddWithValue("@src", src);
                cmd.Parameters.AddWithValue("@from", fromUtc.ToString("o", CultureInfo.InvariantCulture));
                cmd.Parameters.AddWithValue("@to", toUtc.ToString("o", CultureInfo.InvariantCulture));
                var r = cmd.ExecuteScalar();
                if (r is long l) total += (int)Math.Min(l, int.MaxValue);
                else if (r is int i) total += i;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ProductionCountService: GetTotalInRange 查询失败 source={Source}", source);
        }
        return total;
    }

    public Task FlushAsync()
    {
        // SQLite WAL 在连接关闭时自动 checkpoint；我们每个查询/写入都用 using 关闭连接，
        // 这里再做一次显式 checkpoint，保证关闭前数据落盘。
        try
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "ProductionCountService: FlushAsync wal_checkpoint 失败");
        }
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_attached)
        {
            try { _opcUa.TagValueChanged -= HandleTagValueChanged; }
            catch { /* ignore */ }
        }
    }

    // ============================================================
    // 班次时间解析
    // ============================================================

    /// <summary>解析今天白班开始时间（本地时间）。如果配置无效，回退到 08:30。</summary>
    private DateTime ResolveTodayShiftStartLocal()
    {
        var today = DateTime.Now.Date;
        if (TimeSpan.TryParseExact(_shift.DayStart, "h\\:mm", CultureInfo.InvariantCulture, out var ts))
            return today.Add(ts);
        return today.AddHours(8).AddMinutes(30);
    }

    /// <summary>夜班开始相对白班开始的小时偏移。默认 12（08:30 → 20:30）。</summary>
    private int ResolveNightShiftHourOffset()
    {
        if (TimeSpan.TryParseExact(_shift.DayStart, "h\\:mm", CultureInfo.InvariantCulture, out var day) &&
            TimeSpan.TryParseExact(_shift.NightStart, "h\\:mm", CultureInfo.InvariantCulture, out var night))
        {
            var diffHours = (int)Math.Round((night - day).TotalHours);
            if (diffHours < 0) diffHours += 24;
            if (diffHours <= 0 || diffHours >= 24) return 12;
            return diffHours;
        }
        return 12;
    }
}
