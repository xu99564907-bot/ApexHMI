#nullable enable
using System.Linq;
using System.Text;
using ApexHMI.Models.RuntimeUi;

namespace ApexHMI.Services.RuntimeUi;

/// <summary>P6E: 解析 <c>{textList:nameOrId}</c> / <c>{graphicList:nameOrId}</c> 引用为 inline 字符串
/// （<c>0=停止;1=运行;2=报警</c>），让既有 io-symbolic / io-graphic 解析逻辑无须改造即可消费。
/// <para>名称和 Id 二选一匹配；找不到返回原值。</para>
/// </summary>
public static class ListResolver
{
    public const string TextListPrefix = "{textList:";
    public const string GraphicListPrefix = "{graphicList:";

    public static string Resolve(string? value, ListResources? lists)
    {
        if (string.IsNullOrEmpty(value)) return value ?? string.Empty;
        if (lists is null) return value!;

        if (value!.StartsWith(TextListPrefix) && value.EndsWith("}"))
        {
            var name = value.Substring(TextListPrefix.Length, value.Length - TextListPrefix.Length - 1);
            var lst = lists.TextLists.FirstOrDefault(l => l.Name == name || l.Id == name);
            if (lst is null) return value;
            return Inline(lst);
        }

        if (value.StartsWith(GraphicListPrefix) && value.EndsWith("}"))
        {
            var name = value.Substring(GraphicListPrefix.Length, value.Length - GraphicListPrefix.Length - 1);
            var lst = lists.GraphicLists.FirstOrDefault(l => l.Name == name || l.Id == name);
            if (lst is null) return value;
            return Inline(lst);
        }

        return value;
    }

    private static string Inline(TextList lst)
    {
        var sb = new StringBuilder();
        foreach (var it in lst.Items)
        {
            if (sb.Length > 0) sb.Append(';');
            sb.Append(it.Value).Append('=').Append(it.Text);
        }
        return sb.ToString();
    }

    private static string Inline(GraphicList lst)
    {
        var sb = new StringBuilder();
        foreach (var it in lst.Items)
        {
            if (sb.Length > 0) sb.Append(';');
            sb.Append(it.Value).Append('=').Append(it.Image);
        }
        return sb.ToString();
    }

    public static bool IsTextListReference(string? value)
        => !string.IsNullOrEmpty(value) && value!.StartsWith(TextListPrefix) && value.EndsWith("}");

    public static bool IsGraphicListReference(string? value)
        => !string.IsNullOrEmpty(value) && value!.StartsWith(GraphicListPrefix) && value.EndsWith("}");
}
