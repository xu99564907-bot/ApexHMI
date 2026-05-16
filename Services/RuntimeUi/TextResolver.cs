#nullable enable
using System.Linq;
using ApexHMI.Models.RuntimeUi;

namespace ApexHMI.Services.RuntimeUi;

/// <summary>P6B: 解析 <c>{text:keyName}</c> 引用为当前语言文本。
/// <para>查找逻辑：currentLang → DefaultLanguage → 首个非空值 → 原值。</para>
/// </summary>
public static class TextResolver
{
    public const string Prefix = "{text:";

    public static string Resolve(string? value, TextResources? texts, string? currentLang)
    {
        if (string.IsNullOrEmpty(value)) return value ?? string.Empty;
        if (texts is null) return value!;
        if (!value!.StartsWith(Prefix) || !value.EndsWith("}")) return value;

        var key = value.Substring(Prefix.Length, value.Length - Prefix.Length - 1);
        var entry = texts.Entries.FirstOrDefault(e => e.Key == key);
        if (entry is null) return value;

        var lang = currentLang ?? texts.DefaultLanguage;
        if (entry.Values.TryGetValue(lang, out var v) && !string.IsNullOrEmpty(v)) return v;
        if (entry.Values.TryGetValue(texts.DefaultLanguage, out var dv) && !string.IsNullOrEmpty(dv)) return dv;
        // 第一个非空 fallback
        foreach (var kv in entry.Values)
            if (!string.IsNullOrEmpty(kv.Value)) return kv.Value;
        return value;
    }

    public static bool IsTextReference(string? value)
        => !string.IsNullOrEmpty(value) && value!.StartsWith(Prefix) && value.EndsWith("}");
}
