#nullable enable
using System.Linq;
using ApexHMI.Models.RuntimeUi;

namespace ApexHMI.Services.RuntimeUi;

/// <summary>P6A: 解析 <c>{style:colors/...}</c> 和 <c>{style:fonts/...}</c> 引用。
/// <para>调用方拿到的若不是引用语法（普通字面色值如 <c>#2563EB</c>），原样返回。</para>
/// <para>字体引用返回管道分隔串 <c>family|size|weight</c>，由消费方自行拆分（也可调
/// <see cref="ResolveFont"/> 拿到强类型结构）。</para>
/// </summary>
public static class StyleResolver
{
    public const string ColorPrefix = "{style:colors/";
    public const string FontPrefix  = "{style:fonts/";

    /// <summary>解析颜色或字体引用；非引用原样返回。</summary>
    public static string Resolve(string? value, StyleDefinitions? styles)
    {
        if (string.IsNullOrEmpty(value)) return value ?? string.Empty;
        if (styles is null) return value!;

        if (value!.StartsWith(ColorPrefix) && value.EndsWith("}"))
        {
            var key = value.Substring(ColorPrefix.Length, value.Length - ColorPrefix.Length - 1);
            var c = styles.Colors.FirstOrDefault(x => x.Key == key);
            return c?.Value ?? value;
        }

        if (value.StartsWith(FontPrefix) && value.EndsWith("}"))
        {
            var key = value.Substring(FontPrefix.Length, value.Length - FontPrefix.Length - 1);
            var f = styles.Fonts.FirstOrDefault(x => x.Key == key);
            return f is null ? value : $"{f.Family}|{f.Size}|{f.Weight}";
        }

        return value;
    }

    /// <summary>查找字体引用对应的 <see cref="FontPreset"/>；非引用或未命中返回 null。</summary>
    public static FontPreset? ResolveFont(string? value, StyleDefinitions? styles)
    {
        if (string.IsNullOrEmpty(value) || styles is null) return null;
        if (!value!.StartsWith(FontPrefix) || !value.EndsWith("}")) return null;
        var key = value.Substring(FontPrefix.Length, value.Length - FontPrefix.Length - 1);
        return styles.Fonts.FirstOrDefault(x => x.Key == key);
    }

    /// <summary>判定是否为样式引用语法。</summary>
    public static bool IsStyleReference(string? value)
        => !string.IsNullOrEmpty(value) &&
           (value!.StartsWith(ColorPrefix) || value.StartsWith(FontPrefix)) &&
           value.EndsWith("}");
}
