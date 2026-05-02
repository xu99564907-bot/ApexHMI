using ApexHMI;
using Opc.Ua;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ApexHMI.Services.OpcUa;

public sealed class OpcUaResolvedNodeCacheStore
{
    private readonly SemaphoreSlim _ioLock = new(1, 1);
    private readonly string _cachePath;

    public OpcUaResolvedNodeCacheStore(string? applicationRoot = null)
    {
        var appRoot = string.IsNullOrWhiteSpace(applicationRoot)
            ? OpcUaApplicationConfigurationFactory.ResolveApplicationRoot()
            : applicationRoot;
        _cachePath = Path.Combine(appRoot, "config", "opc-resolved-node-cache.json");
    }

    public static string NormalizeEndpointKey(string endpointUrl) =>
        (endpointUrl ?? string.Empty).Trim().ToLowerInvariant();

    public async Task<IReadOnlyDictionary<string, NodeId>> LoadAsync(
        string endpointKey,
        CancellationToken cancellationToken = default)
    {
        var resolved = new Dictionary<string, NodeId>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(endpointKey) || !File.Exists(_cachePath))
        {
            return resolved;
        }

        await _ioLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var json = await Compat.ReadAllTextAsync(_cachePath, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(json))
            {
                return resolved;
            }

            var cache = JsonSerializer.Deserialize<ResolvedNodeCacheFile>(json);
            if (cache?.Endpoints is null
                || !cache.Endpoints.TryGetValue(endpointKey, out var map)
                || map is null)
            {
                return resolved;
            }

            foreach (var pair in map)
            {
                if (string.IsNullOrWhiteSpace(pair.Key) || string.IsNullOrWhiteSpace(pair.Value))
                {
                    continue;
                }

                try
                {
                    resolved[pair.Key] = NodeId.Parse(pair.Value);
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "OPC UA 解析缓存项失败，忽略缓存项。Key={CacheKey}, Value={CacheValue}", pair.Key, pair.Value);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "读取 OPC UA NodeId 解析缓存失败，按动态解析继续。Endpoint={Endpoint}", endpointKey);
        }
        finally
        {
            _ioLock.Release();
        }

        return resolved;
    }

    public async Task SaveAsync(
        string endpointKey,
        IReadOnlyDictionary<string, NodeId> resolvedNodeIds,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(endpointKey))
        {
            return;
        }

        var directory = Path.GetDirectoryName(_cachePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await _ioLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ResolvedNodeCacheFile file;
            if (File.Exists(_cachePath))
            {
                try
                {
                    var existingJson = await Compat.ReadAllTextAsync(_cachePath, cancellationToken).ConfigureAwait(false);
                    file = JsonSerializer.Deserialize<ResolvedNodeCacheFile>(existingJson) ?? new ResolvedNodeCacheFile();
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "读取现有 OPC UA NodeId 缓存文件失败，将重建缓存。Path={Path}", _cachePath);
                    file = new ResolvedNodeCacheFile();
                }
            }
            else
            {
                file = new ResolvedNodeCacheFile();
            }

            file.Endpoints[endpointKey] = resolvedNodeIds
                .Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
                .ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value.ToString(),
                    StringComparer.OrdinalIgnoreCase);

            var json = JsonSerializer.Serialize(file, new JsonSerializerOptions { WriteIndented = true });
            await Compat.WriteAllTextAsync(_cachePath, json, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "写入 OPC UA NodeId 解析缓存失败，忽略缓存写入。Endpoint={Endpoint}", endpointKey);
        }
        finally
        {
            _ioLock.Release();
        }
    }

    private sealed class ResolvedNodeCacheFile
    {
        public Dictionary<string, Dictionary<string, string>> Endpoints { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
