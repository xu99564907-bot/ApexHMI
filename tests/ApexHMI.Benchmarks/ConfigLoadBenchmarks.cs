using System;
using System.IO;
using System.Text;
using ApexHMI.Models;
using ApexHMI.Services;
using BenchmarkDotNet.Attributes;

namespace ApexHMI.Benchmarks;

/// <summary>
/// 基准：appsettings.json 配置加载与反序列化。
/// </summary>
[MemoryDiagnoser]
public class ConfigLoadBenchmarks
{
    private string _configPath = string.Empty;
    private ConfigurationService _service = null!;

    [GlobalSetup]
    public void Setup()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ApexHMI.Benchmarks");
        Directory.CreateDirectory(dir);
        _configPath = Path.Combine(dir, "appsettings.json");

        var config = new AppConfig
        {
            Connection = new() { ServerIp = "127.0.0.1", Port = 4840 },
            Tags = new() { new() { Name = "Tag1", NodeId = "ns=4;s=|var|PLC.Tag1" } },
        };

        var json = System.Text.Json.JsonSerializer.Serialize(config, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_configPath, json, Encoding.UTF8);

        _service = new ConfigurationService();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        try { File.Delete(_configPath); } catch { }
    }

    [Benchmark]
    public AppConfig? LoadConfig()
    {
        return _service.LoadAsync(_configPath).GetAwaiter().GetResult();
    }
}
