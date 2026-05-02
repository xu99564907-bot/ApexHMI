using ApexHMI.Models;
using ApexHMI.Services;
using ApexHMI.Tests.TestHelpers;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace ApexHMI.Tests.Services;

public class CsvHistoryServiceTests
{
    [Fact]
    public async Task TrendHistoryLoadAsyncReturnsEmptyListForMissingFile()
    {
        using var tempDir = TempDir.Create();
        var service = new TrendHistoryService();

        var samples = await service.LoadAsync(Path.Combine(tempDir.Path, "missing.csv"));

        Assert.Empty(samples);
    }

    [Fact]
    public async Task TrendHistoryAppendAsyncCreatesHeaderAndPersistsSamples()
    {
        using var tempDir = TempDir.Create();
        var path = Path.Combine(tempDir.Path, "history", "trend.csv");
        var service = new TrendHistoryService();
        var time = new DateTime(2026, 5, 2, 9, 30, 0);

        await service.AppendAsync(path, new[]
        {
            new TrendSample { Time = time, Category = "Temperature", Value = 12.3456, Source = "SensorA" }
        });

        var lines = File.ReadAllLines(path);
        var loaded = await service.LoadAsync(path);

        Assert.Equal("Time,Category,Value,Source", lines[0]);
        Assert.Single(loaded);
        Assert.Equal(time, loaded[0].Time);
        Assert.Equal("Temperature", loaded[0].Category);
        Assert.Equal(12.346, loaded[0].Value, 3);
        Assert.Equal("SensorA", loaded[0].Source);
    }

    [Fact]
    public async Task TrendHistoryAppendAsyncDoesNotDuplicateHeader()
    {
        using var tempDir = TempDir.Create();
        var path = Path.Combine(tempDir.Path, "trend.csv");
        var service = new TrendHistoryService();

        await service.AppendAsync(path, new[] { new TrendSample { Category = "A", Value = 1, Source = "S1" } });
        await service.AppendAsync(path, new[] { new TrendSample { Category = "B", Value = 2, Source = "S2" } });

        var lines = File.ReadAllLines(path);

        Assert.Equal(3, lines.Length);
        Assert.Single(lines, line => line == "Time,Category,Value,Source");
    }

    [Fact]
    public async Task FlowLogLoadAsyncReturnsEmptyListForMissingFile()
    {
        using var tempDir = TempDir.Create();
        var service = new FlowLogCsvService();

        var records = await service.LoadAsync(Path.Combine(tempDir.Path, "missing.csv"));

        Assert.Empty(records);
    }

    [Fact]
    public async Task FlowLogAppendAsyncEscapesCommaQuoteAndRoundTrips()
    {
        using var tempDir = TempDir.Create();
        var path = Path.Combine(tempDir.Path, "flow", "log.csv");
        var service = new FlowLogCsvService();
        var start = new DateTime(2026, 5, 2, 10, 0, 0);
        var end = start.AddSeconds(3.25);

        await service.AppendAsync(path, new FlowStepRecord
        {
            FlowId = "F1",
            FlowName = "Main,Flow",
            Time = start,
            StartTime = start,
            EndTime = end,
            DurationSeconds = 3.25,
            StepNo = 7,
            Icon = ">",
            Title = "Clamp \"A\"",
            Comment = "ok, next",
            Result = "Done",
            RelatedAlarm = "Alarm,01",
            IsAbnormal = true,
            ShiftKey = "D",
            ArchiveDate = "2026-05-02"
        });

        var loaded = await service.LoadAsync(path);

        Assert.Single(loaded);
        Assert.Equal("Main,Flow", loaded[0].FlowName);
        Assert.Equal("Clamp \"A\"", loaded[0].Title);
        Assert.Equal("ok, next", loaded[0].Comment);
        Assert.Equal("Alarm,01", loaded[0].RelatedAlarm);
        Assert.True(loaded[0].IsAbnormal);
    }

    [Fact]
    public async Task FlowLogAppendAsyncDoesNotDuplicateHeader()
    {
        using var tempDir = TempDir.Create();
        var path = Path.Combine(tempDir.Path, "flow.csv");
        var service = new FlowLogCsvService();

        await service.AppendAsync(path, new FlowStepRecord { FlowId = "F1" });
        await service.AppendAsync(path, new FlowStepRecord { FlowId = "F2" });

        var lines = File.ReadAllLines(path);

        Assert.Equal(3, lines.Length);
        Assert.Single(lines, line => line.StartsWith("FlowId,FlowName,Time,", StringComparison.Ordinal));
    }

    [Fact]
    public async Task FlowLogLoadAsyncParsesQuotedFieldsContainingNewLines()
    {
        using var tempDir = TempDir.Create();
        var path = Path.Combine(tempDir.Path, "flow.csv");
        var service = new FlowLogCsvService();
        var comment = "line one" + Environment.NewLine + "line two";

        await service.AppendAsync(path, new FlowStepRecord
        {
            FlowId = "F1",
            FlowName = "MainFlow",
            Comment = comment,
            Result = "Done"
        });

        var loaded = await service.LoadAsync(path);

        Assert.Single(loaded);
        Assert.Equal(comment, loaded[0].Comment);
        Assert.Equal("Done", loaded[0].Result);
    }
}
