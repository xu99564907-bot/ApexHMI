using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ApexHMI.Models.Production;

namespace ApexHMI.Services.Production;

/// <summary>
/// 生产计数服务。订阅 PLC 累计计数 tag，按差值法落盘到 SQLite，提供分时 / 班次 / 历史查询。
/// 详细设计见 docs/count-module-design.md。
/// </summary>
public interface IProductionCountService
{
    /// <summary>
    /// 订阅 OK.Total / NG.Total tag 值变化。应用启动连接 OPC UA 后调用一次。
    /// 多次调用是幂等的（重复订阅会被去重）。
    /// </summary>
    Task AttachAsync();

    /// <summary>
    /// 显式喂一个 tag 值（用于手工测试或 OPC UA 订阅回调）。
    /// source = "OK" / "NG"，rawValue 是 PLC 累计值字符串。
    /// </summary>
    void OnTagValueChanged(string source, string? rawValue);

    /// <summary>
    /// 今日（按白班开始时间归桶）24 个小时桶。
    /// source: "OK" / "NG" / "Total"（"Total" = OK + NG）。
    /// </summary>
    IReadOnlyList<HourBucket> GetHourlyToday(string source);

    /// <summary>今日白班 / 夜班合计。</summary>
    ShiftTotals GetShiftTotals(string source);

    /// <summary>最近 N 天每天总和（含今天，按本地日期）。</summary>
    IReadOnlyList<DailyTotal> GetDailyHistory(string source, int days);

    /// <summary>任意区间总和（UTC）。</summary>
    int GetTotalInRange(string source, DateTime fromUtc, DateTime toUtc);

    /// <summary>新事件入账后触发，参数是 source（"OK" / "NG"）；UI 据此节流刷新。</summary>
    event Action<string>? EventInserted;

    /// <summary>关闭前 flush，确保 SQLite WAL 刷盘。</summary>
    Task FlushAsync();
}
