using System;
using System.Collections.Generic;
using ApexHMI.Models.RuntimeUi;
using Serilog;

namespace ApexHMI.Services.RuntimeUi;

/// <summary>控件编辑器服务实现。</summary>
public sealed class WidgetEditorService : IWidgetEditorService
{
    private static readonly Dictionary<string, Dictionary<string, string>> DefaultProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        ["text"]   = new() { ["text"] = "文本", ["fontSize"] = "14", ["foreground"] = "#0F172A" },
        ["button"] = new() { ["text"] = "按钮", ["background"] = "#2563EB", ["foreground"] = "#FFFFFF" },
    };

    public WidgetInstance AddWidget(PageDefinition page, string typeId, double x, double y)
    {
        var widget = new WidgetInstance
        {
            TypeId = typeId,
            X = x,
            Y = y,
            Width = GetDefaultWidth(typeId),
            Height = GetDefaultHeight(typeId),
        };

        if (DefaultProperties.TryGetValue(typeId, out var defaults))
        {
            foreach (var kv in defaults)
            {
                widget.Properties[kv.Key] = kv.Value;
            }
        }

        page.Widgets.Add(widget);
        Log.Information("WidgetEditor: 已添加控件 typeId={TypeId} id={Id} x={X} y={Y}", typeId, widget.Id, x, y);
        return widget;
    }

    public bool RemoveWidget(PageDefinition page, string widgetId)
    {
        var widget = page.Widgets.Find(w => string.Equals(w.Id, widgetId, StringComparison.Ordinal));
        if (widget is null) return false;

        page.Widgets.Remove(widget);
        Log.Information("WidgetEditor: 已删除控件 typeId={TypeId} id={Id}", widget.TypeId, widgetId);
        return true;
    }

    public void UpdateProperty(WidgetInstance widget, string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(key)) return;

        if (value is null)
        {
            widget.Properties.Remove(key);
        }
        else
        {
            widget.Properties[key] = value;
        }

        widget.NotifyPropertiesChanged();
    }

    public void UpdateBinding(WidgetInstance widget, BindingSpec? binding)
    {
        widget.Binding = binding;
    }

    public void MoveWidget(WidgetInstance widget, double x, double y)
    {
        widget.X = x;
        widget.Y = y;
    }

    public void ResizeWidget(WidgetInstance widget, double width, double height)
    {
        if (width < 10) width = 10;
        if (height < 10) height = 10;
        widget.Width = width;
        widget.Height = height;
    }

    private static double GetDefaultWidth(string typeId) => typeId.ToLowerInvariant() switch
    {
        "text"   => 160,
        "button" => 120,
        _ => 120,
    };

    private static double GetDefaultHeight(string typeId) => typeId.ToLowerInvariant() switch
    {
        _ => 40,
    };
}
