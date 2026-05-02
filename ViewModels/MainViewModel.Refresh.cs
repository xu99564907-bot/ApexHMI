using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using ApexHMI.Models;
using ApexHMI.Services;
using Serilog;

namespace ApexHMI.ViewModels;

public partial class MainViewModel
{
    // ========== 刷新机制 ==========

    private const int OpcUaSubscriptionMaxItems = 400;
    private static bool EnableRefreshPerfTrace => true;

    private IEnumerable<TagItem> GetTagsForOpcSubscription()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var tag in GetRealtimeSubscriptionTags())
        {
            if (string.IsNullOrWhiteSpace(tag.NodeId))
            {
                continue;
            }

            if (!seen.Add(tag.Name))
            {
                continue;
            }

            yield return tag;
        }
    }

    private IEnumerable<TagItem> GetRealtimeSubscriptionTags()
    {
        // Prefer tags that are in Cylinder category, or have common cylinder-related names
        foreach (var tag in Tags)
        {
            if (string.Equals(tag.Category, "Cylinder", StringComparison.OrdinalIgnoreCase))
            {
                yield return tag;
                continue;
            }

            if (!string.IsNullOrWhiteSpace(tag.Name) && (
                tag.Name.StartsWith("Cylinder_", StringComparison.OrdinalIgnoreCase)
                || tag.Name.IndexOf("Cmd.Manu", StringComparison.OrdinalIgnoreCase) >= 0
                || tag.Name.IndexOf("DevStatus", StringComparison.OrdinalIgnoreCase) >= 0
                || tag.Name.IndexOf("Valve", StringComparison.OrdinalIgnoreCase) >= 0
                || tag.Name.IndexOf("Status.InHome", StringComparison.OrdinalIgnoreCase) >= 0
                || tag.Name.IndexOf("Status.InWork", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                yield return tag;
                continue;
            }

            if (!string.IsNullOrWhiteSpace(tag.NodeId) && (
                tag.NodeId.IndexOf("Cylinder", StringComparison.OrdinalIgnoreCase) >= 0
                || tag.NodeId.IndexOf("Cmd.Manu", StringComparison.OrdinalIgnoreCase) >= 0
                || tag.NodeId.IndexOf("DevStatus", StringComparison.OrdinalIgnoreCase) >= 0
                || tag.NodeId.IndexOf("Status.InHome", StringComparison.OrdinalIgnoreCase) >= 0
                || tag.NodeId.IndexOf("Status.InWork", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                yield return tag;
            }
        }
    }

    /// <summary>
    /// 根据当前页面/子页面，返回需要刷新的 Tags 子集。
    /// 只刷新当前页面相关的变量，其他页面不刷新。
    /// </summary>
    private IEnumerable<TagItem> GetTagsForCurrentPage()
    {
        return SelectedTabIndex switch
        {
            0 => Tags, // 运行总览 — 刷新全部
            1 => Tags, // 监控 — 刷新全部 I/O
            3 => GetManualPageTags(), // 手动操作 — 按子页面过滤
            4 => GetParameterPageTags(), // 参数设定 — 按子页面过滤
            _ => Enumerable.Empty<TagItem>() // 其他页面不刷新
        };
    }

    private IEnumerable<TagItem> GetManualPageTags()
    {
        return CurrentManualSubSection switch
        {
            "气缸" => GetManualCylinderPageTags(),
            "轴" => Tags.Where(t =>
                string.Equals(t.Category, "Axis", StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(t.NodeId) && t.NodeId.IndexOf("AxisCtrl", StringComparison.OrdinalIgnoreCase) >= 0)
                || (!string.IsNullOrWhiteSpace(t.Name) && t.Name.StartsWith("Axis", StringComparison.OrdinalIgnoreCase))),
            "机械手" => Tags.Where(t =>
                string.Equals(t.Category, "Robot", StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(t.Name) && t.Name.IndexOf("Robot", StringComparison.OrdinalIgnoreCase) >= 0)),
            "电机" => Tags.Where(t =>
                string.Equals(t.Category, "Motor", StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(t.Name) && t.Name.IndexOf("Motor", StringComparison.OrdinalIgnoreCase) >= 0)),
            "挡停" => Tags.Where(t =>
                string.Equals(t.Category, "Stopper", StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(t.Name) && t.Name.IndexOf("Stopper", StringComparison.OrdinalIgnoreCase) >= 0)),
            _ => Tags
        };
    }

    private IEnumerable<TagItem> GetParameterPageTags()
    {
        return CurrentParameterSubSection switch
        {
            "气缸参数设定" => GetCylinderRelatedTags(),
            "轴参数设定" => Tags.Where(t =>
                string.Equals(t.Category, "Axis", StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(t.NodeId) && t.NodeId.IndexOf("AxisCtrl", StringComparison.OrdinalIgnoreCase) >= 0)),
            "真空参数设定" => Tags.Where(t =>
                string.Equals(t.Category, "Vacuum", StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(t.NodeId) && t.NodeId.IndexOf("Vacuum", StringComparison.OrdinalIgnoreCase) >= 0)),
            "传感器参数设定" => Tags.Where(t =>
                string.Equals(t.Category, "Sensor", StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(t.NodeId) && t.NodeId.IndexOf("Sensor", StringComparison.OrdinalIgnoreCase) >= 0)),
            // 系统参数页不需要全量轮询，避免 6k+ 标签解析阻塞其它页面刷新。
            _ => Enumerable.Empty<TagItem>()
        };
    }

    private IEnumerable<TagItem> GetCylinderRelatedTags()
    {
        return Tags.Where(t =>
            string.Equals(t.Category, "Cylinder", StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrWhiteSpace(t.NodeId) && t.NodeId.IndexOf("CylCtrl", StringComparison.OrdinalIgnoreCase) >= 0)
            || (!string.IsNullOrWhiteSpace(t.Name) && (
                t.Name.StartsWith("Cylinder_", StringComparison.OrdinalIgnoreCase)
                || t.Name.IndexOf("Cmd.Manu", StringComparison.OrdinalIgnoreCase) >= 0
                || t.Name.IndexOf("DevStatus", StringComparison.OrdinalIgnoreCase) >= 0
                || t.Name.IndexOf("Status.InHome", StringComparison.OrdinalIgnoreCase) >= 0
                || t.Name.IndexOf("Status.InWork", StringComparison.OrdinalIgnoreCase) >= 0
                || t.Name.IndexOf("Valve", StringComparison.OrdinalIgnoreCase) >= 0)));
    }

    /// <summary>
    /// 手动气缸页优先轮询卡片实际使用的绑定点，避免全量 Cylinder 标签首轮解析过慢。
    /// </summary>
    private IEnumerable<TagItem> GetManualCylinderPageTags()
    {
        var blocks = (ManualCylinderBlockCards.Any() ? ManualCylinderBlockCards : ManualCylinderBlocks).ToList();
        if (blocks.Count == 0)
        {
            return GetCylinderRelatedTags();
        }

        var bindingKeys = new List<string>();
        foreach (var block in blocks)
        {
            if (block is null)
            {
                continue;
            }

            var root = ResolveCylinderBlockRoot(block);
            bindingKeys.Add(block.HomeCommandTagName);
            bindingKeys.Add(block.WorkCommandTagName);
            bindingKeys.Add(block.HomeSensorTagName);
            bindingKeys.Add(block.WorkSensorTagName);
            bindingKeys.Add(block.HomeInterlockTagName);
            bindingKeys.Add(block.WorkInterlockTagName);

            if (!string.IsNullOrWhiteSpace(root))
            {
                bindingKeys.Add($"{root}.DevStatus.Valve_Home");
                bindingKeys.Add($"{root}.DevStatus.Valve_Work");
                bindingKeys.Add($"{root}.Status.InHome");
                bindingKeys.Add($"{root}.Status.InWork");
                bindingKeys.Add($"{root}.Status.Error");
                bindingKeys.Add($"{root}.Status.ErrorID");
            }
        }

        // 兼容旧版总览指示位。
        bindingKeys.Add("Cylinder_FwdLS");
        bindingKeys.Add("Cylinder_BwdLS");
        bindingKeys.Add("Cylinder_Extend");

        return ResolveTagsByBindings(bindingKeys);
    }

    private IEnumerable<TagItem> ResolveTagsByBindings(IEnumerable<string> bindings)
    {
        var keys = bindings
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Select(key => key.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var seenTagNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in keys)
        {
            var tag = FindTagByNameOrNodeId(key);
            if (tag is null || !seenTagNames.Add(tag.Name))
            {
                continue;
            }

            yield return tag;
        }
    }

    /// <summary>
    /// 手动操作页需要保持整页轮询：仅依赖订阅时，动作后可能出现写入成功但页面状态不跟随的问题。
    /// 因此手动页不剔除订阅项，始终按当前页变量做整体 Read。
    /// 参数页中的设备子页也保持整体轮询，避免绑定诊断/状态展示滞后。
    /// </summary>
    private bool ShouldForceFullPageReadDespiteOpcSubscription()
    {
        if (SelectedTabIndex == 3)
        {
            return true;
        }

        if (SelectedTabIndex == 4 && !string.Equals(CurrentParameterSubSection, "系统参数设定", StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }

    private async Task AutoRefreshTickAsync()
    {
        if (!AutoRefreshEnabled || !_opcUaService.IsConnected) return;
        if (_isRefreshing) return;

        var tickSw = Stopwatch.StartNew();
        var section = SelectedTabIndex == 3
            ? $"手动/{CurrentManualSubSection}"
            : SelectedTabIndex == 4
                ? $"参数/{CurrentParameterSubSection}"
                : CurrentSection;

        try
        {
            _isRefreshing = true;
            var subscribed = UseOpcSubscription ? _opcUaService.SubscribedTagNames : null;
            var selectSw = Stopwatch.StartNew();
            var pageTagsAll = GetTagsForCurrentPage().ToList();
            var pageTags = subscribed is { Count: > 0 } && !ShouldForceFullPageReadDespiteOpcSubscription()
                ? pageTagsAll.Where(t => !subscribed.Contains(t.Name)).ToList()
                : pageTagsAll;
            selectSw.Stop();

            if (pageTags.Count == 0)
            {
                // 本页无轮询项（可能已全部交给订阅）时，仍要刷新派生状态与绑定展示（含气缸参数诊断）
                UpdateRuntimeVisuals();
                RefreshMonitorView();
                RefreshAlarmStatistics();
                if (IsMonitorIoPageVisible)
                {
                    RefreshIoMonitorStates();
                }
                TraceRefreshPerf($"Tick section={section} tags=0 selectMs={selectSw.ElapsedMilliseconds} totalMs={tickSw.ElapsedMilliseconds} (no-page-tags)");
                return;
            }

            var readSw = Stopwatch.StartNew();
            var values = await _opcUaService.ReadTagsAsync(pageTags);
            readSw.Stop();
            var eventTagNames = BuildEventTagNameSet();
            var processSw = Stopwatch.StartNew();
            var okCount = 0;
            var unresolvedCount = 0;
            var errorCount = 0;
            BeginBatchAlarmEvaluation();
            try
            {
                foreach (var tag in pageTags)
                {
                    if (values.TryGetValue(tag.Name, out var value))
                    {
                        if (string.Equals(value, "ERR: UnresolvedNode", StringComparison.Ordinal))
                        {
                            unresolvedCount++;
                        }
                        else if (value.StartsWith("ERR:", StringComparison.OrdinalIgnoreCase))
                        {
                            errorCount++;
                        }
                        else
                        {
                            okCount++;
                        }

                        OnPlcReadAppliedToTag(tag, value);
                        if (tag.IsAlarm)
                        {
                            EvaluateTagState(tag);
                        }

                        if (eventTagNames.Contains(tag.Name))
                        {
                            EvaluateEvents(tag);
                        }
                    }
                }
            }
            finally
            {
                EndBatchAlarmEvaluation();
            }
            processSw.Stop();

            UpdateRuntimeVisuals();
            RefreshMonitorView();
            RefreshAlarmStatistics();
            if (IsMonitorIoPageVisible)
            {
                RefreshIoMonitorStates();
            }

            TraceRefreshPerf(
                $"Tick section={section} tags={pageTags.Count}/{pageTagsAll.Count} " +
                $"ok={okCount} unresolved={unresolvedCount} err={errorCount} " +
                $"selectMs={selectSw.ElapsedMilliseconds} readMs={readSw.ElapsedMilliseconds} processMs={processSw.ElapsedMilliseconds} totalMs={tickSw.ElapsedMilliseconds}");
        }
        catch (Exception ex)
        {
            SystemMessage = $"页面变量刷新失败：{ex.Message}";
            TraceRefreshPerf($"Tick section={section} exception={ex.Message} totalMs={tickSw.ElapsedMilliseconds}");
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private async Task UpdateAutoRefreshStateAsync()
    {
        try
        {
            _subscriptionTimer.Stop();
            _opcUaBrowserRefreshTimer.Stop();
            await _opcUaService.UnsubscribeAllAsync();
            TraceRefreshPerf($"UpdateAutoRefreshState enter connected={_opcUaService.IsConnected} autoRefresh={AutoRefreshEnabled} useSub={UseOpcSubscription}");

            if (!_opcUaService.IsConnected || !AutoRefreshEnabled) return;

            // 先启动轮询，避免订阅初始化较慢时首屏长时间无刷新。
            var pollMs = Math.Max(500, RefreshIntervalMs);
            _subscriptionTimer.Interval = TimeSpan.FromMilliseconds(pollMs);
            _subscriptionTimer.Start();
            _opcUaBrowserRefreshTimer.Start();

            if (UseOpcSubscription)
            {
                var subTags = GetTagsForOpcSubscription().Take(OpcUaSubscriptionMaxItems).ToList();
                if (subTags.Count > 0)
                {
                    try
                    {
                        var subSw = Stopwatch.StartNew();
                        await _opcUaService.SubscribeTagsAsync(subTags, 250, CancellationToken.None);
                        subSw.Stop();
                        TraceRefreshPerf($"Subscribe init tags={subTags.Count} elapsedMs={subSw.ElapsedMilliseconds}");
                    }
                    catch (Exception ex)
                    {
                        SystemMessage = $"OPC UA 订阅初始化失败（将仅轮询刷新）：{ex.Message}";
                        AddLog("通讯", SystemMessage, "Warning");
                        TraceRefreshPerf($"Subscribe failed tags={subTags.Count} ex={ex.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            SystemMessage = $"自动刷新初始化失败：{ex.Message}";
            AddLog("通讯", SystemMessage, "Error");
        }
    }

    private async Task RefreshCylinderFeedbackTagsAsync()
    {
        var feedbackTags = Tags
            .Where(tag => !string.IsNullOrWhiteSpace(tag.NodeId)
                && (
                    tag.NodeId.EndsWith(".DevStatus.Valve_Home", StringComparison.OrdinalIgnoreCase)
                    || tag.NodeId.EndsWith(".DevStatus.Valve_Work", StringComparison.OrdinalIgnoreCase)
                    || tag.NodeId.EndsWith(".Status.InHome", StringComparison.OrdinalIgnoreCase)
                    || tag.NodeId.EndsWith(".Status.InWork", StringComparison.OrdinalIgnoreCase)
                ))
            .ToList();

        if (feedbackTags.Count == 0)
        {
            return;
        }

        var values = await _opcUaService.ReadTagsAsync(feedbackTags);
        foreach (var tag in feedbackTags)
        {
            if (values.TryGetValue(tag.Name, out var value))
            {
                OnPlcReadAppliedToTag(tag, value);
            }
        }

        UpdateRuntimeVisuals();
        RefreshCylinderBindingProperties();
    }

    private async Task AutoRefreshSelectedOpcUaNodeTickAsync()
    {
        if (!AutoRefreshEnabled || !_opcUaService.IsConnected || SelectedOpcUaBrowseNode is null || SelectedOpcUaBrowseNode.IsPlaceholder)
        {
            return;
        }

        await RefreshSelectedOpcUaNodeAsync();
    }

    private async void OpcUaService_TagValueChanged(string tagNameOrNodeId, string value)
    {
        try
        {
            await RunOnUiThreadAsync(() =>
            {
                var tag = FindTagByNodeId(tagNameOrNodeId) ?? FindTagByNameOrNodeId(tagNameOrNodeId);
                if (tag is not null)
                {
                    OnPlcReadAppliedToTag(tag, value);
                }
                else if (!string.IsNullOrWhiteSpace(tagNameOrNodeId))
                {
                    RecordOpcBindingString(tagNameOrNodeId.Trim(), value);
                }

                if (tag is null)
                {
                    return;
                }

                if (tag.IsAlarm)
                {
                    EvaluateTagState(tag);
                }

                if (HasEventBindingForTag(tag.Name))
                {
                    EvaluateEvents(tag);
                }
                UpdateRuntimeVisuals();
                RefreshCylinderBindingProperties();
                RefreshCylinderMaskStates();
                RefreshMonitorView();
                RefreshAlarmStatistics();
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "OpcUaService_TagValueChanged 异常 tag={Tag}", tagNameOrNodeId);
        }
    }

    [RelayCommand]
    private async Task RefreshTagsAsync()
    {
        if (_isRefreshing) return;
        try
        {
            _isRefreshing = true;
            var values = await _opcUaService.ReadTagsAsync(Tags);
            var eventTagNames = BuildEventTagNameSet();
            BeginBatchAlarmEvaluation();
            try
            {
                foreach (var tag in Tags)
                {
                    if (values.TryGetValue(tag.Name, out var value))
                    {
                        OnPlcReadAppliedToTag(tag, value);
                        if (tag.IsAlarm)
                        {
                            EvaluateTagState(tag);
                        }

                        if (eventTagNames.Contains(tag.Name))
                        {
                            EvaluateEvents(tag);
                        }
                    }
                }
            }
            finally
            {
                EndBatchAlarmEvaluation();
            }
            UpdateRuntimeVisuals();
            RefreshMonitorView();
            UpdateRuntimeVisuals();
            RefreshAlarmStatistics();
            SimulateFlowProgress();
            await SaveTrendHistoryAsync();
        SystemMessage = "变量刷新完成";
        }
        catch (Exception ex)
        {
            SystemMessage = $"变量刷新失败：{ex.Message}";
            AddLog("监控", SystemMessage, "Error");
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private HashSet<string> BuildEventTagNameSet()
    {
        return EventBindings
            .Where(binding => !string.IsNullOrWhiteSpace(binding.TagName))
            .Select(binding => binding.TagName.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private bool HasEventBindingForTag(string? tagName)
    {
        if (string.IsNullOrWhiteSpace(tagName))
        {
            return false;
        }

        var key = tagName.Trim();
        return EventBindings.Any(binding => binding.TagName.Equals(key, StringComparison.OrdinalIgnoreCase));
    }

    private void TraceRefreshPerf(string message)
    {
        if (!EnableRefreshPerfTrace)
        {
            return;
        }

        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "refresh-perf.log");
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}{Environment.NewLine}";
            File.AppendAllText(path, line, Encoding.UTF8);
        }
        catch
        {
            // no-op
        }
    }
}
