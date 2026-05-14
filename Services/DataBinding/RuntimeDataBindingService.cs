using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ApexHMI.Interfaces;
using ApexHMI.Models;
using ApexHMI.Models.RuntimeUi;
using ApexHMI.Services;
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
    private readonly TrendHistoryService? _trendHistory;
    private readonly HashSet<string> _subscribedTagIds = new(StringComparer.OrdinalIgnoreCase);
    private DynamicPageHostViewModel? _activeHost;

    public RuntimeDataBindingService(IDataPointCatalog catalog, IOpcUaService opcUa, TrendHistoryService? trendHistory = null)
    {
        _catalog = catalog;
        _opcUa = opcUa;
        _trendHistory = trendHistory;
        // M3.1: 统一走 quality 路径；OPC UA service 同时触发 string 事件，但本服务只订阅 quality 事件
        // 避免双重 push / 双重历史归档。PushSimulatedValue 仍走老 PushTagValue 流程（默认 Good 质量）。
        _opcUa.TagValueQualityChanged += OnTagValueQualityChanged;
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

    /// <summary>M3.2: 同步等待确认 + 超时。失败/超时返回结构化结果，调用方负责通知用户与审计。</summary>
    public async Task<WriteTagResult> WriteAsyncWithConfirm(string tagId, object value, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var tag = _catalog.FindTag(tagId);
        if (tag is null)
        {
            return WriteTagResult.Fail($"未找到 Tag: {tagId}", TimeSpan.Zero);
        }
        return await _opcUa.WriteTagWithConfirmAsync(tag, value, timeout, cancellationToken).ConfigureAwait(false);
    }

    private void OnTagValueQualityChanged(string tagName, string rawValue, TagQuality quality)
    {
        _activeHost?.PushTagValueWithQuality(tagName, rawValue, quality);
        if (_trendHistory is not null
            && double.TryParse(rawValue, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var dv))
        {
            // Bad quality 仍写到历史，但调用方可后续过滤；保持当前归档语义不变。
            _trendHistory.LogValue(tagName, dv);
        }
    }

    // M3.1: 原 OnTagValueChanged 已被 OnTagValueQualityChanged 取代（OPC UA service 同时触发两事件）。

    /// <summary>P10F: 离线模拟服务推送假值（绕过 OPC UA）。</summary>
    public void PushSimulatedValue(string tagName, string rawValue)
    {
        _activeHost?.PushTagValue(tagName, rawValue);
    }
}
