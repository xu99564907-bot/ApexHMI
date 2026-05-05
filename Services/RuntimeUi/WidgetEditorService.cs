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
        ["text"] = new() { ["text"] = "文本", ["fontSize"] = "14", ["foreground"] = "#0F172A" },
        ["bool-lamp"] = new() { ["label"] = "指示灯", ["trueColor"] = "#22C55E", ["falseColor"] = "#EF4444" },
        ["numeric-readonly"] = new() { ["label"] = "数值", ["unit"] = "", ["format"] = "0.00" },
        ["button"] = new() { ["text"] = "按钮", ["background"] = "#2563EB", ["foreground"] = "#FFFFFF" },
        ["motor"] = new() { ["label"] = "电机", ["runningColor"] = "#22C55E", ["stoppedColor"] = "#64748B" },
        ["cylinder"] = new() { ["label"] = "气缸", ["homeColor"] = "#22C55E", ["workColor"] = "#3B82F6" },
        ["axis"] = new() { ["label"] = "轴", ["unit"] = "mm", ["format"] = "0.00" },
        ["robot"] = new() { ["label"] = "机械手", ["busyColor"] = "#F59E0B", ["idleColor"] = "#64748B" },
        ["stopper"] = new() { ["label"] = "挡停", ["upColor"] = "#EF4444", ["downColor"] = "#22C55E" },
        ["alarm-banner"] = new() { ["label"] = "报警条", ["activeColor"] = "#EF4444", ["inactiveColor"] = "#64748B" },
        ["page-button"] = new() { ["text"] = "页面跳转", ["background"] = "#6366F1", ["foreground"] = "#FFFFFF" },
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
        "text" => 160,
        "bool-lamp" => 160,
        "numeric-readonly" => 200,
        "button" => 120,
        "motor" => 180,
        "cylinder" => 180,
        "axis" => 210,
        "robot" => 190,
        "stopper" => 170,
        "alarm-banner" => 280,
        "page-button" => 140,
        _ => 120,
    };

    private static double GetDefaultHeight(string typeId) => typeId.ToLowerInvariant() switch
    {
        "alarm-banner" => 60,
        "motor" => 100,
        "cylinder" => 90,
        "axis" => 100,
        "robot" => 100,
        "stopper" => 80,
        _ => 40,
    };
}
