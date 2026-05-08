using System;

namespace ApexHMI.Models.Production;

/// <summary>
/// 单个小时桶（按班次开始时间归桶，不一定是 0~23）。CountView 显示每行一个桶。
/// </summary>
public sealed record HourBucket(
    int Index,                    // 0..23
    string TimeRangeText,         // 例 "08:30-09:30"
    DateTime BucketStartLocal,    // 桶起点（本地时间）
    int Count);                   // 该小时累计数量

/// <summary>白班 / 夜班合计。</summary>
public sealed record ShiftTotals(int Day, int Night);

/// <summary>历史日合计。</summary>
public sealed record DailyTotal(DateTime Date, int Count);
