using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ApexHMI.Models;
using ApexHMI.Models.RuntimeUi;

namespace ApexHMI.Interfaces;

public interface IOpcUaService
{
    bool IsConnected { get; }
    string ConnectionStatus { get; }
    /// <summary>Names of tags currently covered by OPC UA subscription (case-insensitive). Empty if none.</summary>
    IReadOnlyCollection<string> SubscribedTagNames { get; }
    event Action<string, string>? TagValueChanged;

    /// <summary>M3.1: 带 quality+timestamp 的值变化事件，运行时把 OPC UA StatusCode 映射到 TagQuality 推上 UI。</summary>
    event Action<string, string, TagQuality>? TagValueQualityChanged;

    /// <summary>M3.2: 写 PLC 结果（成功/失败/超时）。</summary>
    Task<WriteTagResult> WriteTagWithConfirmAsync(TagItem tag, object value, TimeSpan timeout, CancellationToken cancellationToken = default);

    Task ConnectAsync(OpcUaConnectionOptions options, CancellationToken cancellationToken = default);
    Task DisconnectAsync(CancellationToken cancellationToken = default);
    Task<Dictionary<string, string>> ReadTagsAsync(IEnumerable<TagItem> tags);
    Task<Dictionary<string, string>> ReadNodeValuesAsync(IEnumerable<string> nodeIds);
    Task<(string Value, string DataType, string StatusCode, string Timestamp)> ReadNodeAsync(string nodeId);
    Task<IReadOnlyList<OpcUaBrowseNode>> BrowseNodeAsync(string? nodeId = null);
    Task WarmupNodeIdsAsync(IEnumerable<string> nodeIds, CancellationToken cancellationToken = default);
    Task WriteTagAsync(TagItem tag, object value);
    Task SubscribeTagsAsync(IEnumerable<TagItem> tags, int publishingInterval = 500, CancellationToken cancellationToken = default);
    Task UnsubscribeAllAsync(CancellationToken cancellationToken = default);
}
