using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ApexHMI.Services.OpcUa;
using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Configuration;
using ApexHMI.Interfaces;
using ApexHMI.Models;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Context;

namespace ApexHMI.Services;

public class OpcUaService : IOpcUaService
{
    private readonly OpcUaRuntimeOptions _runtimeOptions;
    private Session? _session;
    private ApplicationConfiguration? _configuration;
    private Subscription? _subscription;
    private readonly ConcurrentDictionary<string, MonitoredItem> _monitoredItems = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, NodeId> _resolvedNodeIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, byte> _unresolvedNodeIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, byte> _subscribedTagNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly OpcUaResolvedNodeCacheStore _resolvedNodeCacheStore = new();
    private string _activeEndpointKey = string.Empty;
    private PreferredApplicationSymbolTemplate? _preferredApplicationSymbolTemplate;

    public OpcUaService(IOptions<OpcUaRuntimeOptions>? runtimeOptions = null)
    {
        _runtimeOptions = runtimeOptions?.Value ?? new OpcUaRuntimeOptions();
    }

    public bool IsConnected => _session?.Connected == true;
    public string ConnectionStatus => IsConnected ? "已连接" : "未连接";
    public IReadOnlyCollection<string> SubscribedTagNames => _subscribedTagNames.Keys.ToArray();
    public event Action<string, string>? TagValueChanged;

    public async Task ConnectAsync(OpcUaConnectionOptions options, CancellationToken cancellationToken = default)
    {
        using var _ = LogContext.PushProperty("CorrelationId", Guid.NewGuid().ToString("N"));
        var sw = Stopwatch.StartNew();
        try
        {
            _activeEndpointKey = OpcUaResolvedNodeCacheStore.NormalizeEndpointKey(options.GetEndpointUrl());
            _configuration = await OpcUaApplicationConfigurationFactory.BuildAsync().ConfigureAwait(false);
            await DisconnectAsync(cancellationToken).ConfigureAwait(false);
            _resolvedNodeIds.Clear();
            _unresolvedNodeIds.Clear();
            _subscribedTagNames.Clear();
            ClearPreferredApplicationSymbolTemplate();

            Exception? directAttemptException = null;

            try
            {
                _session = await WithTimeoutAsync(
                    ct => OpcUaSessionFactory.CreateDirectAsync(_configuration, options.GetEndpointUrl(), options, ct),
                    _runtimeOptions.ConnectAttemptTimeoutMs,
                    cancellationToken).ConfigureAwait(false);
                await MergeResolvedNodeCacheAsync(cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (Exception ex)
            {
                directAttemptException = ex;
                Log.Warning(ex, "OPC UA 直连尝试失败，Endpoint={Endpoint}", options.GetEndpointUrl());
                // Fallback to discovery-based attempts below.
            }

            var connectionAttempts = BuildConnectionAttempts(options);
            Exception? lastException = null;
            var attemptErrors = new List<string>();

            foreach (var attempt in connectionAttempts)
            {
                try
                {
                    _session = await WithTimeoutAsync(
                        ct => OpcUaSessionFactory.CreateAsync(_configuration, attempt.EndpointUrl, attempt.UseSecurity, options, ct),
                        _runtimeOptions.ConnectAttemptTimeoutMs,
                        cancellationToken).ConfigureAwait(false);
                    await MergeResolvedNodeCacheAsync(cancellationToken).ConfigureAwait(false);
                    return;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    Log.Warning(ex, "OPC UA 连接尝试失败，Endpoint={Endpoint}, Security={Security}", attempt.EndpointUrl, attempt.UseSecurity);
                    attemptErrors.Add($"{attempt.EndpointUrl} (security={(attempt.UseSecurity ? "on" : "off")}): {ex.Message}");
                }
            }

            var detailBuilder = new StringBuilder();
            if (directAttemptException is not null)
            {
                detailBuilder.AppendLine($"direct: {options.GetEndpointUrl()} (security=off): {directAttemptException.Message}");
            }

            foreach (var attemptError in attemptErrors.Take(6))
            {
                detailBuilder.AppendLine(attemptError);
            }

            throw new InvalidOperationException(
                $"无法连接到 OPC UA 服务器：{options.GetEndpointUrl()}。请检查 Endpoint、匿名权限或安全策略设置。{Environment.NewLine}{detailBuilder.ToString().TrimEnd()}",
                lastException);
        }
        finally
        {
            Log.Information("OPC UA 连接完成 elapsedMs={ElapsedMs} connected={Connected}", sw.ElapsedMilliseconds, IsConnected);
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(_activeEndpointKey) && _resolvedNodeIds.Count > 0)
        {
            await _resolvedNodeCacheStore.SaveAsync(_activeEndpointKey, _resolvedNodeIds, cancellationToken).ConfigureAwait(false);
        }

        try
        {
            await WithTimeoutAsync(
                ct => UnsubscribeAllAsync(ct),
                5000,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex)
        {
            Log.Debug(ex, "OPC UA 取消订阅超时或取消，继续断开会话");
            // Continue tearing down the session.
        }

        _resolvedNodeIds.Clear();
        _unresolvedNodeIds.Clear();
        _subscribedTagNames.Clear();
        ClearPreferredApplicationSymbolTemplate();
        if (_session is null)
        {
            return;
        }

        var session = _session;
        _session = null;

        try
        {
            await WithTimeoutAsync(
                ct => session.CloseAsync(true, ct),
                5000,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex)
        {
            Log.Debug(ex, "OPC UA 关闭会话超时或取消，继续释放资源");
            // Proceed to dispose.
        }

        session.Dispose();
        _activeEndpointKey = string.Empty;
    }

    public async Task<Dictionary<string, string>> ReadTagsAsync(IEnumerable<TagItem> tags)
    {
        if (_session is null || !_session.Connected)
        {
            return tags.ToDictionary(t => t.Name, _ => "未连接");
        }

        var totalSw = Stopwatch.StartNew();
        var result = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var tagList = tags.Where(t => !string.IsNullOrWhiteSpace(t.NodeId)).ToList();
        var resolvedSlots = new (NodeId NodeId, TagItem Tag)?[tagList.Count];

        var resolveSw = Stopwatch.StartNew();
        using var resolveLimiter = new SemaphoreSlim(_runtimeOptions.ResolveParallelism, _runtimeOptions.ResolveParallelism);
        var resolveTasks = Enumerable.Range(0, tagList.Count).Select(async i =>
        {
            var tag = tagList[i];
            await resolveLimiter.WaitAsync().ConfigureAwait(false);
            try
            {
                var nodeId = await ResolveNodeIdAsync(tag.NodeId).ConfigureAwait(false);
                resolvedSlots[i] = (nodeId, tag);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "OPC UA Tag NodeId 解析失败，Tag={TagName}, NodeId={NodeId}", tag.Name, tag.NodeId);
                result[tag.Name] = "ERR: UnresolvedNode";
            }
            finally
            {
                resolveLimiter.Release();
            }
        });

        await Task.WhenAll(resolveTasks).ConfigureAwait(false);
        resolveSw.Stop();

        var readIds = new ReadValueIdCollection();
        var resolvedTags = new List<TagItem>();
        for (var i = 0; i < resolvedSlots.Length; i++)
        {
            if (resolvedSlots[i] is not { } pair)
            {
                continue;
            }

            readIds.Add(new ReadValueId { NodeId = pair.NodeId, AttributeId = Attributes.Value });
            resolvedTags.Add(pair.Tag);
        }

        if (readIds.Count == 0)
        {
            // nothing resolved, return what we have
            foreach (var tag in tags)
            {
                result.TryAdd(tag.Name, string.Empty);
            }

            TraceOpcPerf($"ReadTags count={tagList.Count} resolved=0 unresolved={result.Count} resolveMs={resolveSw.ElapsedMilliseconds} totalMs={totalSw.ElapsedMilliseconds} endpoint={_activeEndpointKey}");
            return new Dictionary<string, string>(result, StringComparer.OrdinalIgnoreCase);
        }

        var readSw = Stopwatch.StartNew();
        try
        {
            // Single batched read
            var response = await _session.ReadAsync(null, 0, TimestampsToReturn.Neither, readIds, default).ConfigureAwait(false);
            Debug.Assert(response.Results.Count == readIds.Count,
                $"OPC UA response count ({response.Results.Count}) does not match request count ({readIds.Count})");

            for (var i = 0; i < resolvedTags.Count; i++)
            {
                var tag = resolvedTags[i];
                var dataValue = response.Results[i];
                if (StatusCode.IsBad(dataValue.StatusCode))
                {
                    result[tag.Name] = $"ERR: {dataValue.StatusCode}";
                }
                else
                {
                    result[tag.Name] = dataValue.Value?.ToString() ?? string.Empty;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "OPC UA 批量读取 Tag 失败");
            foreach (var tag in tags)
            {
                result.TryAdd(tag.Name, $"ERR: {ex.Message}");
            }
        }
        readSw.Stop();

        var unresolvedCount = result.Values.Count(v => string.Equals(v, "ERR: UnresolvedNode", StringComparison.Ordinal));
        var errorCount = result.Values.Count(v => v.StartsWith("ERR:", StringComparison.OrdinalIgnoreCase) && !string.Equals(v, "ERR: UnresolvedNode", StringComparison.Ordinal));
        TraceOpcPerf(
            $"ReadTags count={tagList.Count} resolved={resolvedTags.Count} unresolved={unresolvedCount} error={errorCount} " +
            $"resolveMs={resolveSw.ElapsedMilliseconds} readMs={readSw.ElapsedMilliseconds} totalMs={totalSw.ElapsedMilliseconds} endpoint={_activeEndpointKey}");

        return new Dictionary<string, string>(result, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<Dictionary<string, string>> ReadNodeValuesAsync(IEnumerable<string> nodeIds)
    {
        var requestedNodeIds = nodeIds
            .Where(nodeId => !string.IsNullOrWhiteSpace(nodeId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (_session is null || !_session.Connected)
        {
            return requestedNodeIds.ToDictionary(nodeId => nodeId, _ => "ERR: NotConnected", StringComparer.OrdinalIgnoreCase);
        }

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var resolvedRequests = new List<(string RawNodeId, NodeId ResolvedNodeId)>();

        foreach (var rawNodeId in requestedNodeIds)
        {
            try
            {
                resolvedRequests.Add((rawNodeId, await ResolveNodeIdAsync(rawNodeId)));
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "OPC UA NodeId 解析失败，NodeId={NodeId}", rawNodeId);
                result[rawNodeId] = "ERR: UnresolvedNode";
            }
        }

        if (resolvedRequests.Count == 0)
        {
            return result;
        }

        var readIds = new ReadValueIdCollection();
        foreach (var request in resolvedRequests)
        {
            readIds.Add(new ReadValueId
            {
                NodeId = request.ResolvedNodeId,
                AttributeId = Attributes.Value
            });
        }

        try
        {
            var response = await _session.ReadAsync(null, 0, TimestampsToReturn.Neither, readIds, default);
            for (var i = 0; i < resolvedRequests.Count; i++)
            {
                var dataValue = response.Results[i];
                var rawNodeId = resolvedRequests[i].RawNodeId;
                result[rawNodeId] = StatusCode.IsBad(dataValue.StatusCode)
                    ? $"ERR: {dataValue.StatusCode}"
                    : dataValue.Value?.ToString() ?? string.Empty;
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "OPC UA 批量读取 NodeId 失败");
            foreach (var request in resolvedRequests)
            {
                result.TryAdd(request.RawNodeId, $"ERR: {ex.Message}");
            }
        }

        return result;
    }

    public async Task<(string Value, string DataType, string StatusCode, string Timestamp)> ReadNodeAsync(string nodeId)
    {
        if (_session is null || !_session.Connected)
        {
            throw new InvalidOperationException("OPC UA 未连接。");
        }

        var dataValue = await _session.ReadValueAsync(await ResolveNodeIdAsync(nodeId));
        var dataType = dataValue.WrappedValue.TypeInfo?.BuiltInType.ToString() ?? dataValue.Value?.GetType().Name ?? "--";
        var timestamp = dataValue.SourceTimestamp == DateTime.MinValue
            ? "--"
            : dataValue.SourceTimestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

        return (
            dataValue.Value?.ToString() ?? string.Empty,
            dataType,
            dataValue.StatusCode.ToString(),
            timestamp);
    }

    public async Task WarmupNodeIdsAsync(IEnumerable<string> nodeIds, CancellationToken cancellationToken = default)
    {
        if (_session is null || !_session.Connected)
        {
            return;
        }

        var inputs = nodeIds
            .Where(nodeId => !string.IsNullOrWhiteSpace(nodeId))
            .Select(nodeId => nodeId.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (inputs.Count == 0)
        {
            return;
        }

        using var limiter = new SemaphoreSlim(_runtimeOptions.ResolveParallelism, _runtimeOptions.ResolveParallelism);
        var tasks = inputs.Select(async nodeId =>
        {
            await limiter.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await ResolveNodeIdAsync(nodeId).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "OPC UA 预热 NodeId 失败，NodeId={NodeId}", nodeId);
                // 预解析阶段允许个别节点失败，运行时会按原逻辑继续回退探测。
            }
            finally
            {
                limiter.Release();
            }
        });

        await Task.WhenAll(tasks).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(_activeEndpointKey) && _resolvedNodeIds.Count > 0)
        {
            await _resolvedNodeCacheStore.SaveAsync(_activeEndpointKey, _resolvedNodeIds, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<IReadOnlyList<OpcUaBrowseNode>> BrowseNodeAsync(string? nodeId = null)
    {
        if (_session is null || !_session.Connected)
        {
            throw new InvalidOperationException("OPC UA 未连接。");
        }

        var targetNodeId = string.IsNullOrWhiteSpace(nodeId) ? ObjectIds.ObjectsFolder : NodeId.Parse(nodeId);
        var browser = new Browser(_session)
        {
            BrowseDirection = BrowseDirection.Forward,
            ReferenceTypeId = ReferenceTypeIds.HierarchicalReferences,
            IncludeSubtypes = true,
            NodeClassMask = (int)(NodeClass.Object | NodeClass.Variable | NodeClass.Method | NodeClass.View)
        };

        var references = await browser.BrowseAsync(targetNodeId);
        var nodes = references
            .Select(reference =>
            {
                var resolvedNodeId = ExpandedNodeId.ToNodeId(reference.NodeId, _session.NamespaceUris);
                var node = new OpcUaBrowseNode
                {
                    DisplayName = reference.DisplayName.Text ?? reference.BrowseName.Name ?? resolvedNodeId?.ToString() ?? "(Unnamed)",
                    NodeId = resolvedNodeId?.ToString() ?? string.Empty,
                    NodeClass = reference.NodeClass.ToString(),
                    HasChildren = reference.NodeClass is NodeClass.Object or NodeClass.Variable or NodeClass.View
                };

                if (node.HasChildren)
                {
                    node.Children.Add(OpcUaBrowseNode.CreatePlaceholder());
                }

                return node;
            })
            .Where(node => !string.IsNullOrWhiteSpace(node.NodeId))
            .OrderBy(node => node.NodeClass)
            .ThenBy(node => node.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return nodes;
    }

    public async Task WriteTagAsync(TagItem tag, object value)
    {
        if (_session is null || !_session.Connected)
        {
            throw new InvalidOperationException("OPC UA 未连接。");
        }

        var writeValue = new WriteValue
        {
            NodeId = await ResolveNodeIdAsync(tag.NodeId),
            AttributeId = Attributes.Value,
            Value = new DataValue(new Variant(value))
        };

        var collection = new WriteValueCollection { writeValue };
        var response = await _session.WriteAsync(null, collection, default);
        ClientBase.ValidateResponse(response.Results, collection);
        ClientBase.ValidateDiagnosticInfos(response.DiagnosticInfos, collection);
    }

    public async Task SubscribeTagsAsync(IEnumerable<TagItem> tags, int publishingInterval = 500, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            if (_session is null || !_session.Connected)
            {
                return;
            }

            var tagList = tags.Where(t => !string.IsNullOrWhiteSpace(t.NodeId)).ToList();
            if (tagList.Count == 0)
            {
                await UnsubscribeAllAsync(cancellationToken).ConfigureAwait(false);
                return;
            }

            await UnsubscribeAllAsync(cancellationToken).ConfigureAwait(false);

            _subscription = new Subscription(_session.DefaultSubscription)
            {
                PublishingInterval = publishingInterval,
                DisplayName = "ApexHMISubscription"
            };

            _session.AddSubscription(_subscription);
            await _subscription.CreateAsync(cancellationToken).ConfigureAwait(false);

            foreach (var tag in tagList)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var parsedNodeId = await ResolveNodeIdAsync(tag.NodeId).ConfigureAwait(false);

                    var monitoredItem = new MonitoredItem(_subscription.DefaultItem)
                    {
                        DisplayName = tag.NodeId,
                        StartNodeId = parsedNodeId,
                        AttributeId = Attributes.Value,
                        SamplingInterval = publishingInterval,
                        QueueSize = 1,
                        DiscardOldest = true
                    };

                    monitoredItem.Notification += (_, _) =>
                    {
                        foreach (var value in monitoredItem.DequeueValues())
                        {
                            TagValueChanged?.Invoke(tag.Name, value.Value?.ToString() ?? string.Empty);
                        }
                    };

                    _subscription.AddItem(monitoredItem);
                    _monitoredItems[tag.NodeId] = monitoredItem;
                    _subscribedTagNames[tag.Name] = 0;
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "OPC UA 订阅 Tag 失败，Tag={TagName}, NodeId={NodeId}", tag.Name, tag.NodeId);
                    continue;
                }
            }

            try
            {
                await _subscription.ApplyChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                ClearSubscriptionState();
                throw;
            }
        }
        finally
        {
            Log.Information("OPC UA 订阅初始化完成 elapsedMs={ElapsedMs} count={Count}", sw.ElapsedMilliseconds, _subscribedTagNames.Count);
        }
    }

    public async Task UnsubscribeAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_subscription is not null && _session is not null)
        {
            try
            {
                var subscription = _subscription;
                await subscription.DeleteAsync(true, cancellationToken).ConfigureAwait(false);
                await _session.RemoveSubscriptionAsync(subscription, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "OPC UA 清理订阅失败，继续释放本地状态");
                // Ignore cleanup errors.
            }
        }

        }
        finally
        {
            ClearSubscriptionState();
        }
    }

    private void ClearSubscriptionState()
    {
        _monitoredItems.Clear();
        _subscription = null;
        _subscribedTagNames.Clear();
    }

    private static async Task WithTimeoutAsync(Func<CancellationToken, Task> action, int timeoutMs, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeoutMs);
        await action(cts.Token).ConfigureAwait(false);
    }

    private static async Task<T> WithTimeoutAsync<T>(Func<CancellationToken, Task<T>> action, int timeoutMs, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeoutMs);
        return await action(cts.Token).ConfigureAwait(false);
    }

    /// <summary>
    /// 去掉从界面整段复制时误带的底栏等杂串（如主窗口的 “C# / WPF / OPC UA / HMI Designer”）。
    /// </summary>
    private static string NormalizeOpcUaNodeIdText(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var s = raw.Trim();
        string[] junkMarkers =
        {
            "C# / WPF / OPC UA / HMI Designer",
            "C# / WPF / OPC UA",
            "C# / WPF",
        };

        foreach (var m in junkMarkers)
        {
            var i = s.IndexOf(m, StringComparison.Ordinal);
            if (i < 0)
            {
                continue;
            }

            s = s[..i].TrimEnd();
            break;
        }

        return s;
    }

    private async Task<NodeId> ResolveNodeIdAsync(string nodeIdText)
    {
        nodeIdText = NormalizeOpcUaNodeIdText(nodeIdText);
        if (string.IsNullOrWhiteSpace(nodeIdText))
        {
            throw new InvalidOperationException("NodeId 为空。");
        }

        if (IsObviouslyInvalidLegacyPath(nodeIdText))
        {
            _unresolvedNodeIds.TryAdd(nodeIdText, 0);
            throw new InvalidOperationException($"无效的旧版标签路径：{nodeIdText}");
        }

        if (_resolvedNodeIds.TryGetValue(nodeIdText, out var cached))
        {
            return cached;
        }

        if (_unresolvedNodeIds.ContainsKey(nodeIdText))
        {
            throw new InvalidOperationException($"未能解析 OPC UA NodeId：{nodeIdText}");
        }

        if (TryBuildPreferredApplicationNodeId(nodeIdText, out var preferredNodeId))
        {
            _resolvedNodeIds[nodeIdText] = preferredNodeId;
            return preferredNodeId;
        }

        try
        {
            var parsed = NodeId.Parse(nodeIdText);
            _resolvedNodeIds[nodeIdText] = parsed;
            return parsed;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "OPC UA NodeId 直接解析失败，继续尝试符号路径候选：{NodeId}", nodeIdText);
            // Continue to symbol path candidates.
        }

        if (_session is null || !_session.Connected)
        {
            throw new InvalidOperationException($"未能解析 OPC UA NodeId：{nodeIdText}");
        }

        Exception? lastException = null;
        foreach (var candidateText in BuildCandidateNodeIdTexts(nodeIdText).Take(_runtimeOptions.MaxNodeIdProbeCandidates))
        {
            try
            {
                var candidateNodeId = NodeId.Parse(candidateText);
                var dataValue = await _session.ReadValueAsync(candidateNodeId).ConfigureAwait(false);
                // ReadValueAsync 对无效 NodeId 常返回 Bad 状态而不抛异常，必须校验状态否则误缓存首个候选项。
                if (StatusCode.IsBad(dataValue.StatusCode))
                {
                    lastException = new ServiceResultException(dataValue.StatusCode);
                    continue;
                }

                _resolvedNodeIds[nodeIdText] = candidateNodeId;
                LearnPreferredApplicationSymbolTemplate(nodeIdText, candidateNodeId);
                return candidateNodeId;
            }
            catch (Exception ex)
            {
                lastException = ex;
                Log.Debug(ex, "OPC UA NodeId 候选探测失败，Raw={RawNodeId}, Candidate={CandidateNodeId}", nodeIdText, candidateText);
            }
        }

        _unresolvedNodeIds.TryAdd(nodeIdText, 0);
        TraceOpcPerf($"ResolveFail raw={nodeIdText} candidates={_runtimeOptions.MaxNodeIdProbeCandidates} endpoint={_activeEndpointKey}");
        throw new InvalidOperationException($"未能解析 OPC UA NodeId：{nodeIdText}", lastException);
    }

    private static bool IsObviouslyInvalidLegacyPath(string nodeIdText)
    {
        // 历史错误样式：CylCtrl.DB...Application[n] / VacCtrl.DB...Application[n] / AxisCtrl.DB...Application[n]
        // 正确应为 Application.DBxxxx_DriveControl.<Type>Ctrl[n].*
        var startsWithInvalidPrefix =
            nodeIdText.StartsWith("CylCtrl.DB", StringComparison.OrdinalIgnoreCase)
            || nodeIdText.StartsWith("VacCtrl.DB", StringComparison.OrdinalIgnoreCase)
            || nodeIdText.StartsWith("AxisCtrl.DB", StringComparison.OrdinalIgnoreCase);

        return startsWithInvalidPrefix
            && nodeIdText.Contains(".Application[", StringComparison.OrdinalIgnoreCase);
    }

    private bool TryBuildPreferredApplicationNodeId(string rawNodeIdText, out NodeId nodeId)
    {
        nodeId = NodeId.Null;
        if (!rawNodeIdText.StartsWith("Application.", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var template = _preferredApplicationSymbolTemplate;
        if (template is null)
        {
            return false;
        }

        nodeId = new NodeId($"{template.SymbolPrefix}{rawNodeIdText}", template.NamespaceIndex);
        return true;
    }

    private void LearnPreferredApplicationSymbolTemplate(string rawNodeIdText, NodeId candidateNodeId)
    {
        if (!rawNodeIdText.StartsWith("Application.", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (candidateNodeId.IdType != IdType.String || candidateNodeId.Identifier is not string identifier)
        {
            return;
        }

        var markerIndex = identifier.IndexOf(rawNodeIdText, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
        {
            return;
        }

        var prefix = identifier[..markerIndex];
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return;
        }

        _preferredApplicationSymbolTemplate = new PreferredApplicationSymbolTemplate(
            candidateNodeId.NamespaceIndex,
            prefix);

        // 首次学到模板后清掉 Application 失败缓存，避免早期失败项长期阻塞后续刷新。
        foreach (var unresolvedKey in _unresolvedNodeIds.Keys)
        {
            if (unresolvedKey.StartsWith("Application.", StringComparison.OrdinalIgnoreCase))
            {
                _unresolvedNodeIds.TryRemove(unresolvedKey, out _);
            }
        }
    }

    private void ClearPreferredApplicationSymbolTemplate()
    {
        _preferredApplicationSymbolTemplate = null;
    }

    private IEnumerable<string> BuildCandidateNodeIdTexts(string rawText)
    {
        var normalized = rawText.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            yield break;
        }

        // 先尝试最常见命名空间；再补充会话发现的命名空间（限制数量，避免首次解析过慢）。
        var namespaceIndexes = new[] { 4, 3, 2, 1, 0 }
            .Concat(Enumerable.Range(0, _session?.NamespaceUris.Count ?? 0).Reverse())
            .Distinct()
            .Take(8)
            .ToList();

        foreach (var pathBase in EnumerateApplicationPathBases(normalized))
        {
            var symbolCandidates = new List<string>();
            if (pathBase.Contains('.', StringComparison.Ordinal))
            {
                if (pathBase.StartsWith("Application.", StringComparison.OrdinalIgnoreCase))
                {
                    // 不同固件/导出可能为 |var| 或 |appo|（例：ns=4;s=|var|Inovance-PLC.Application...），优先试 |var|
                    symbolCandidates.Add($"|var|Inovance-PLC.{pathBase}");
                    symbolCandidates.Add($"|appo|Inovance-PLC.{pathBase}");
                    symbolCandidates.Add($"|vprop|Inovance-PLC.{pathBase}");
                    symbolCandidates.Add($"Inovance-PLC.{pathBase}");
                    symbolCandidates.Add($"|vprop|{pathBase}");
                }

                symbolCandidates.Add(pathBase);

                if (!pathBase.StartsWith("|var|", StringComparison.Ordinal))
                {
                    symbolCandidates.Add($"|var|{pathBase}");
                }
            }

            // 先固定符号、遍历命名空间：更快命中同一符号在不同 ns 的常见映射。
            foreach (var symbolCandidate in symbolCandidates.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                foreach (var namespaceIndex in namespaceIndexes)
                {
                    yield return $"ns={namespaceIndex};s={symbolCandidate}";
                }
            }
        }
    }

    /// <summary>
    /// 为 Application 下结构化数组（如 CylCtrl[1]）生成多种地址形式，适配不同 OPC UA 栈的浏览名习惯（方括号/0 基/点号等）。
    /// </summary>
    private static IEnumerable<string> EnumerateApplicationPathBases(string normalized)
    {
        yield return normalized;
        foreach (var alt in ExpandStructuredArraySegments(normalized).Take(4))
        {
            if (!string.Equals(alt, normalized, StringComparison.Ordinal))
            {
                yield return alt;
            }
        }
    }

    private static IEnumerable<string> ExpandStructuredArraySegments(string path)
    {
        // 例：...CylCtrl[1] -> CylCtrl[0]（0 基）、CylCtrl.1、CylCtrl(1)
        var m = Regex.Match(path, @"\.(?<name>CylCtrl|AxisCtrl|VacCtrl)\[(?<idx>\d+)\]", RegexOptions.IgnoreCase);
        if (!m.Success || !int.TryParse(m.Groups["idx"].Value, out var index))
        {
            yield break;
        }

        var name = m.Groups["name"].Value;
        var segment = m.Value;
        yield return path.Replace(segment, $".{name}.{index}", StringComparison.Ordinal);
        yield return path.Replace(segment, $".{name}({index})", StringComparison.Ordinal);
        if (index > 0)
        {
            yield return path.Replace(segment, $".{name}[{index - 1}]", StringComparison.Ordinal);
            yield return path.Replace(segment, $".{name}.{index - 1}", StringComparison.Ordinal);
        }
    }

    private static IReadOnlyList<(string EndpointUrl, bool UseSecurity)> BuildConnectionAttempts(OpcUaConnectionOptions options)
    {
        var attempts = new List<(string EndpointUrl, bool UseSecurity)>();
        var baseUrl = options.GetEndpointUrl();
        attempts.Add((baseUrl, false));

        if (string.IsNullOrWhiteSpace(options.EndpointPath))
        {
            var discoveryUrl = baseUrl.EndsWith("/", StringComparison.OrdinalIgnoreCase)
                ? baseUrl + "discovery"
                : baseUrl + "/discovery";
            if (!attempts.Any(item => item.EndpointUrl.Equals(discoveryUrl, StringComparison.OrdinalIgnoreCase) && item.UseSecurity == false))
            {
                attempts.Add((discoveryUrl, false));
            }
        }

        if (!attempts.Any(item => item.EndpointUrl.Equals(baseUrl, StringComparison.OrdinalIgnoreCase) && item.UseSecurity))
        {
            attempts.Add((baseUrl, true));
        }

        return attempts;
    }

    private async Task MergeResolvedNodeCacheAsync(CancellationToken cancellationToken)
    {
        var cachedNodeIds = await _resolvedNodeCacheStore.LoadAsync(_activeEndpointKey, cancellationToken).ConfigureAwait(false);
        foreach (var pair in cachedNodeIds)
        {
            _resolvedNodeIds[pair.Key] = pair.Value;
        }
    }

    private void TraceOpcPerf(string message)
    {
        if (!_runtimeOptions.EnablePerformanceTrace)
        {
            return;
        }

        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "opc-perf.log");
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}{Environment.NewLine}";
            File.AppendAllText(path, line, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "写入 OPC UA 性能追踪失败");
            // ignore trace failure
        }
    }

    private sealed record PreferredApplicationSymbolTemplate(ushort NamespaceIndex, string SymbolPrefix);
}
