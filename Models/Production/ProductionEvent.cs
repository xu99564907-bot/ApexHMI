using System;

namespace ApexHMI.Models.Production;

/// <summary>
/// 生产计数事件：每次 PLC 端 Total 增加时记录一条，写入 SQLite。
/// 差值法核心：HMI 不依赖 PLC 自维护的"今日 / 班次 / 31 天"账本，
/// 仅订阅 OK.Total / NG.Total 累计数，差值入账。
/// </summary>
public sealed record ProductionEvent(
    long Id,
    DateTime TsUtc,
    string Source,        // "OK" / "NG"
    int Delta,            // 本次新增数量（正整数）
    long CounterValue,    // PLC 当时累计值
    string? Note);        // "reset" / "gap_recovery" / null
