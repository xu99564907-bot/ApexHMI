using System;
using System.Collections.Generic;
using System.Linq;
using ApexHMI.Models;

namespace ApexHMI.Services;

/// <summary>
/// 刷新策略协调器 —— 封装 OPC 订阅候选筛选、页面级轮询标签集计算以及强刷决策。
/// 所有方法均为纯函数，不持有 UI 状态，便于单独测试与策略替换。
/// </summary>
public sealed class RefreshCoordinator
{
    /// <summary>
    /// 返回应加入 OPC UA 订阅的高频标签候选集。
    /// 优先气缸/CylCtrl/手动命令/阀状态等需要毫秒级响应的变量。
    /// </summary>
    public IEnumerable<TagItem> GetSubscriptionCandidates(IEnumerable<TagItem> tags)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var tag in GetRealtimeSubscriptionTags(tags))
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

    private static IEnumerable<TagItem> GetRealtimeSubscriptionTags(IEnumerable<TagItem> tags)
    {
        foreach (var tag in tags)
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
    /// 返回当前页面/子页面需要轮询刷新的标签集。
    /// </summary>
    public IEnumerable<TagItem> GetTagsForCurrentPage(
        IEnumerable<TagItem> tags,
        int selectedTabIndex,
        string currentManualSubSection,
        string currentParameterSubSection,
        Func<IEnumerable<TagItem>, IEnumerable<TagItem>>? manualPageResolver = null)
    {
        return selectedTabIndex switch
        {
            0 => tags, // 运行总览 — 刷新全部
            1 => tags, // 监控 — 刷新全部 I/O
            3 => GetManualPageTags(tags, currentManualSubSection, manualPageResolver), // 手动操作
            4 => GetParameterPageTags(tags, currentParameterSubSection), // 参数设定
            _ => Enumerable.Empty<TagItem>()
        };
    }

    private static IEnumerable<TagItem> GetManualPageTags(
        IEnumerable<TagItem> tags,
        string subSection,
        Func<IEnumerable<TagItem>, IEnumerable<TagItem>>? manualPageResolver)
    {
        return subSection switch
        {
            "气缸" => manualPageResolver?.Invoke(tags) ?? GetCylinderRelatedTags(tags),
            "轴" => tags.Where(t =>
                string.Equals(t.Category, "Axis", StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(t.NodeId) && t.NodeId.IndexOf("AxisCtrl", StringComparison.OrdinalIgnoreCase) >= 0)
                || (!string.IsNullOrWhiteSpace(t.Name) && t.Name.StartsWith("Axis", StringComparison.OrdinalIgnoreCase))),
            "机械手" => tags.Where(t =>
                string.Equals(t.Category, "Robot", StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(t.Name) && t.Name.IndexOf("Robot", StringComparison.OrdinalIgnoreCase) >= 0)),
            "电机" => tags.Where(t =>
                string.Equals(t.Category, "Motor", StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(t.Name) && t.Name.IndexOf("Motor", StringComparison.OrdinalIgnoreCase) >= 0)),
            "挡停" => tags.Where(t =>
                string.Equals(t.Category, "Stopper", StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(t.Name) && t.Name.IndexOf("Stopper", StringComparison.OrdinalIgnoreCase) >= 0)),
            _ => tags
        };
    }

    private static IEnumerable<TagItem> GetParameterPageTags(
        IEnumerable<TagItem> tags,
        string subSection)
    {
        return subSection switch
        {
            "气缸参数设定" => GetCylinderRelatedTags(tags),
            "轴参数设定" => tags.Where(t =>
                string.Equals(t.Category, "Axis", StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(t.NodeId) && t.NodeId.IndexOf("AxisCtrl", StringComparison.OrdinalIgnoreCase) >= 0)),
            "真空参数设定" => tags.Where(t =>
                string.Equals(t.Category, "Vacuum", StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(t.NodeId) && t.NodeId.IndexOf("Vacuum", StringComparison.OrdinalIgnoreCase) >= 0)),
            "传感器参数设定" => tags.Where(t =>
                string.Equals(t.Category, "Sensor", StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(t.NodeId) && t.NodeId.IndexOf("Sensor", StringComparison.OrdinalIgnoreCase) >= 0)),
            _ => Enumerable.Empty<TagItem>()
        };
    }

    /// <summary>
    /// 获取所有气缸相关标签（按 Category / NodeId / Name 前缀匹配）。
    /// </summary>
    public static IEnumerable<TagItem> GetCylinderRelatedTags(IEnumerable<TagItem> tags)
    {
        return tags.Where(t =>
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
    /// 判断当前页是否需要强制全量轮询（即使在有 OPC 订阅的情况下）。
    /// 手动操作页与设备参数子页需要整页轮询以保证写入后状态实时跟随。
    /// </summary>
    public bool ShouldForceFullPageReadDespiteOpcSubscription(
        int selectedTabIndex,
        string currentParameterSubSection)
    {
        if (selectedTabIndex == 3)
        {
            return true;
        }

        if (selectedTabIndex == 4 && !string.Equals(currentParameterSubSection, "系统参数设定", StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }
}
