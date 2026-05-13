#nullable enable
using System.Collections.Generic;

namespace ApexHMI.Services.RuntimeUi;

/// <summary>
/// P7B: Faceplate 内部 widget 属性的 <c>{prop:keyName}</c> 引用解析。
/// 当内部 widget 的 Properties[X] = "{prop:displayName}" 时，根据
/// Faceplate 实例当前的 PropertyValues["displayName"] 返回实际值。
/// </summary>
public static class FaceplateResolver
{
    public static string Resolve(string? value, IReadOnlyDictionary<string, string>? propertyValues)
    {
        if (string.IsNullOrEmpty(value) || propertyValues is null) return value ?? string.Empty;
        if (!value.StartsWith("{prop:") || !value.EndsWith("}")) return value;
        var key = value.Substring(6, value.Length - 7);
        return propertyValues.TryGetValue(key, out var v) ? v : value;
    }
}
