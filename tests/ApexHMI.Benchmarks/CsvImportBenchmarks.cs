using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ApexHMI.Models;
using ApexHMI.Services;
using BenchmarkDotNet.Attributes;

namespace ApexHMI.Benchmarks;

/// <summary>
/// 基准：CSV IO 表导入（1 万行）。
/// </summary>
[MemoryDiagnoser]
public class CsvImportBenchmarks
{
    private string _tempCsvPath = string.Empty;
    private IoTableImportService _service = null!;

    [GlobalSetup]
    public void Setup()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ApexHMI.Benchmarks");
        Directory.CreateDirectory(dir);
        _tempCsvPath = Path.Combine(dir, $"bench-{Guid.NewGuid():N}.csv");

        var sb = new StringBuilder();
        sb.AppendLine("输入模块,输入地址,输入工位,输入变量注释,输入备注,输出模块,输出地址,输出工位,输出变量注释,输出备注");
        for (var i = 0; i < 10_000; i++)
        {
            sb.AppendLine($"模块{i % 10},%IX{i}.0,工位{i % 5},输入信号{i},备注{i},模块{i % 10},%QX{i}.0,工位{i % 5},输出信号{i},备注{i}");
        }

        File.WriteAllText(_tempCsvPath, sb.ToString(), new UTF8Encoding(true));

        _service = new IoTableImportService(new IoTableParser());
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        try { File.Delete(_tempCsvPath); } catch { }
    }

    [Benchmark]
    public IoTableImportResult Import10kRows()
    {
        return _service.ImportAsync(_tempCsvPath).GetAwaiter().GetResult();
    }
}
