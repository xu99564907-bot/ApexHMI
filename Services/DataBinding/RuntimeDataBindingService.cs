using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ApexHMI.Interfaces;
using ApexHMI.Models;
using ApexHMI.ViewModels.Runtime;
using Serilog;

namespace ApexHMI.Services.DataBinding;

/// <summary>
/// 运行时数据绑定服务。
/// 为当前可见页面的所有 BoundTagIds 建立 OPC UA 订阅；
/// 监听 IOpcUaService.TagValueChanged 并回调到 DynamicPageHostViewModel.PushTagValue。
/// </summary>
public class RuntimeDataBindingService
{
    private readonly IDataPointCatalog _catalog;
    private readonly IOpcUaService _opcUa;
    private readonly HashSet<string> _subscribedTagIds = new(StringComparer.OrdinalIgnoreCase);
    private DynamicPageHostViewModel? _activeHost;

    public RuntimeDataBindingService(IDataPointCatalog catalog, IOpcUaService opcUa)
    {
        _catalog = catalog;
        _opcUa = opcUa;
        _opcUa.TagValueChanged += OnTagValueChanged;
    }

    /// <summary>为指定宿主页面的所有绑定 TagId 建立 OPC UA 订阅（已订阅的不重复）。</summary>
    public async Task AttachAsync(DynamicPageHostViewModel host)
    {
        _activeHost = host;

        var newTagIds = host.BoundTagIds
            .Where(id => !string.IsNullOrWhiteSpace(id) && !_subscribedTagIds.Contains(id))
            .ToList();

        var newTags = new List<TagItem>();
        foreach (var tagId in newTagIds)
        {
            var tag = _catalog.FindTag(tagId);
            if (tag is null)
            {
                Log.Debug("RuntimeDataBinding: 未在目录中找到 TagId={TagId}，跳过订阅", tagId);
                continue;
            }

            _subscribedTagIds.Add(tagId);
            newTags.Add(tag);
        }

        if (newTags.Count > 0)
        {
            await _opcUa.SubscribeTagsAsync(newTags);
        }
    }

    /// <summary>主动写入一个 Tag 值。</summary>
    public async Task WriteAsync(string tagId, object value)
    {
        var tag = _catalog.FindTag(tagId);
        if (tag is null) return;
        await _opcUa.WriteTagAsync(tag, value);
    }

    private void OnTagValueChanged(string tagName, string rawValue)
    {
        _activeHost?.PushTagValue(tagName, rawValue);
    }

    /// <summary>P10F: 离线模拟服务推送假值（绕过 OPC UA）。</summary>
    public void PushSimulatedValue(string tagName, string rawValue)
    {
        _activeHost?.PushTagValue(tagName, rawValue);
    }
}
