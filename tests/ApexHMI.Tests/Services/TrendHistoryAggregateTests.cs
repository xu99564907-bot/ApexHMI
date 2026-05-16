#nullable enable
using System;
using System.IO;
using System.Reflection;
using ApexHMI.Services;
using FluentAssertions;
using Xunit;

namespace ApexHMI.Tests.Services;

/// <summary>
/// M7.5: TrendHistoryService.QueryAggregated SQL bucket 聚合验证。
/// 直接用反射改 DbPath 静态字段到临时目录，避开污染生产 data/trend_history.db。
/// </summary>
public sealed class TrendHistoryAggregateTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string? _originalDbPath;

    public TrendHistoryAggregateTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ApexHMI.TrendAggTests." + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        var field = typeof(TrendHistoryService).GetField("DbPath",
            BindingFlags.NonPublic | BindingFlags.Static);
        _originalDbPath = field?.GetValue(null) as string;
        field?.SetValue(null, Path.Combine(_tempDir, "trend_history.db"));
    }

    public void Dispose()
    {
        var field = typeof(TrendHistoryService).GetField("DbPath",
            BindingFlags.NonPublic | BindingFlags.Static);
        if (_originalDbPath is not null) field?.SetValue(null, _originalDbPath);
        try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    [Fact]
    public void QueryAggregated_GroupsSamplesByBucket()
    {
        var svc = new TrendHistoryService();
        svc.EnableLogging("tagA", 1000);

        // 写 100 个点，间隔 1s，跨 100 秒
        var baseTime = DateTime.UtcNow.AddSeconds(-200);
        for (int i = 0; i < 100; i++)
        {
            svc.LogValue("tagA", i * 1.0, baseTime.AddSeconds(i));
        }

        // 1) 原始查询：100 点
        var raw = svc.Query("tagA", baseTime.AddSeconds(-1), baseTime.AddSeconds(101));
        raw.Count.Should().Be(100);

        // 2) 60s bucket：100s 跨度 → 约 2 个桶（具体取决于 bucket 对齐）
        var agg60 = svc.QueryAggregated("tagA", baseTime.AddSeconds(-1), baseTime.AddSeconds(101), 60_000);
        agg60.Count.Should().BeLessOrEqualTo(3, "100s 跨度 60s 桶最多 3 个对齐桶");
        agg60.Count.Should().BeGreaterOrEqualTo(2);

        // 3) bucketMs = 0 → 等价 Query
        var agg0 = svc.QueryAggregated("tagA", baseTime.AddSeconds(-1), baseTime.AddSeconds(101), 0);
        agg0.Count.Should().Be(100);

        // 4) 大 bucket（120s）→ 应 ≤ 2 个桶
        var agg120 = svc.QueryAggregated("tagA", baseTime.AddSeconds(-1), baseTime.AddSeconds(101), 120_000);
        agg120.Count.Should().BeLessOrEqualTo(2);
    }
}
