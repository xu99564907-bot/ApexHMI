using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ApexHMI.Models;

namespace ApexHMI.Interfaces;

public interface IOpcUaService
{
    bool IsConnected { get; }
    string ConnectionStatus { get; }
    /// <summary>Names of tags currently covered by OPC UA subscription (case-insensitive). Empty if none.</summary>
    IReadOnlyCollection<string> SubscribedTagNames { get; }
    event Action<string, string>? TagValueChanged;

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
