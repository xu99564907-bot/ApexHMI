using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using ApexHMI.Services.OpcUa;
using BenchmarkDotNet.Attributes;
using Opc.Ua;

namespace ApexHMI.Benchmarks;

/// <summary>
/// 基准：OPC UA NodeId 解析缓存的读写。
/// </summary>
[MemoryDiagnoser]
public class NodeCacheBenchmarks
{
    private OpcUaResolvedNodeCacheStore _store = null!;
    private Dictionary<string, NodeId> _nodeIds = null!;
    private string _endpointKey = "opc.tcp://localhost:4840";

    [GlobalSetup]
    public void Setup()
    {
        var root = Path.Combine(Path.GetTempPath(), "ApexHMI.Benchmarks", $"cache-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "config"));
        _store = new OpcUaResolvedNodeCacheStore(root);

        _nodeIds = new Dictionary<string, NodeId>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < 500; i++)
        {
            _nodeIds[$"ns=4;s=|var|PLC.Tag{i}"] = new NodeId($"|var|PLC.Tag{i}", 4);
        }
    }

    [Benchmark]
    public IReadOnlyDictionary<string, NodeId> SaveAndLoad()
    {
        _store.SaveAsync(_endpointKey, _nodeIds, CancellationToken.None).GetAwaiter().GetResult();
        return _store.LoadAsync(_endpointKey, CancellationToken.None).GetAwaiter().GetResult();
    }

    [Benchmark]
    public IReadOnlyDictionary<string, NodeId> LoadOnly()
    {
        // 先存一次保证文件存在
        _store.SaveAsync(_endpointKey, _nodeIds, CancellationToken.None).GetAwaiter().GetResult();
        return _store.LoadAsync(_endpointKey, CancellationToken.None).GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        try
        {
            var root = Path.Combine(Path.GetTempPath(), "ApexHMI.Benchmarks");
            foreach (var dir in Directory.GetDirectories(root, "cache-*"))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
        catch { }
    }
}
