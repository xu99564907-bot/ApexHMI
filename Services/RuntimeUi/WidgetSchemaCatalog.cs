#nullable enable
using System;
using System.Collections.Generic;
using ApexHMI.Models.RuntimeUi;

namespace ApexHMI.Services.RuntimeUi;

/// <summary>
/// P7.5: 静态 Widget Schema 目录。
/// <para>属性面板调用 <see cref="Lookup"/> 查询某 typeId 的 Schema；找不到则回退到旧 generic 编辑器。</para>
/// <para>当前覆盖 10 个高频 widget；中频 17 个待补（见 TODO 列表）。</para>
/// </summary>
public static class WidgetSchemaCatalog
{
    /// <summary>typeId → schema 的全局表（不区分大小写）。</summary>
    private static readonly Dictionary<string, WidgetSchema> All =
        new(StringComparer.OrdinalIgnoreCase);

    static WidgetSchemaCatalog()
    {
        // P7.5C: 注入 10 个高频 widget schema 种子（text/rectangle/ellipse/button/io-numeric/
        // io-symbolic/switch/bar/gauge/trend-view）。其余 17 个中频 widget 走 fallback，待 v2.0 补齐。
        WidgetSchemaCatalogSeed.Seed(All);
    }

    /// <summary>查找 typeId 对应的 Schema。找不到时返回 null（调用方走 fallback）。</summary>
    public static WidgetSchema? Lookup(string? typeId)
    {
        if (string.IsNullOrWhiteSpace(typeId)) return null;
        return All.TryGetValue(typeId, out var schema) ? schema : null;
    }

    /// <summary>当前已注册的 typeId 数量（用于 dev 自检）。</summary>
    public static int RegisteredCount => All.Count;

    /// <summary>列出所有已注册的 typeId（debug 用）。</summary>
    public static IEnumerable<string> RegisteredTypeIds => All.Keys;
}
