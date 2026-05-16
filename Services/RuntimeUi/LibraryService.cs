#nullable enable
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using ApexHMI.Models.RuntimeUi;

namespace ApexHMI.Services.RuntimeUi;

/// <summary>P6C: 库公用工具 — Widget 深拷贝（JSON 序列化往返）。</summary>
public static class LibraryService
{
    private static readonly JsonSerializerOptions _opt = new()
    {
        WriteIndented = false,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>用 JSON 往返做 WidgetInstance 深拷贝。
    /// <para>新实例会获得新 Id（由 <see cref="WidgetInstance"/> 默认值生成）。</para>
    /// </summary>
    public static WidgetInstance CloneWidget(WidgetInstance src)
    {
        var json = JsonSerializer.Serialize(src, _opt);
        var copy = JsonSerializer.Deserialize<WidgetInstance>(json, _opt) ?? new WidgetInstance();
        copy.Id = System.Guid.NewGuid().ToString("N");
        return copy;
    }
}
