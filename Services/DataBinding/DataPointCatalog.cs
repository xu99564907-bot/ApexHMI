using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ApexHMI.Models;
using Serilog;

namespace ApexHMI.Services.DataBinding;

/// <summary>
/// 数据点目录实现。
/// 从 config/tags.json 加载 TagItem 列表；也可通过 Merge 接受外部来源更新。
/// 页面中的逻辑 TagId 对应 TagItem.Name。
/// </summary>
public class DataPointCatalog : IDataPointCatalog
{
    private readonly Dictionary<string, TagItem> _byName = new(StringComparer.OrdinalIgnoreCase);

    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public DataPointCatalog()
    {
        TryLoadFromConfigFile();
    }

    private void TryLoadFromConfigFile()
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config", "tags.json");
        if (!File.Exists(path))
            return;

        try
        {
            var json = File.ReadAllText(path, System.Text.Encoding.UTF8);
            var cfg = JsonSerializer.Deserialize<TagsConfigShim>(json, _jsonOptions);
            if (cfg?.Tags is not null)
            {
                Merge(cfg.Tags);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "DataPointCatalog 从 {Path} 加载 tags.json 失败，将跳过该文件", path);
        }
    }

    /// <summary>将外部 Tag 列表合并进目录（来自 MainViewModel 加载的完整配置）。</summary>
    public void Merge(IEnumerable<TagItem> tags)
    {
        foreach (var t in tags)
        {
            if (!string.IsNullOrWhiteSpace(t.Name))
                _byName[t.Name] = t;
        }
    }

    public TagItem? FindTag(string tagId)
    {
        if (string.IsNullOrWhiteSpace(tagId))
            return null;
        return _byName.TryGetValue(tagId, out var t) ? t : null;
    }

    public IEnumerable<TagItem> GetAll() => _byName.Values;

    private class TagsConfigShim
    {
        public List<TagItem>? Tags { get; set; }
    }
}
