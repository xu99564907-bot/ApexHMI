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
        // 基本对象
        ["text"]   = new()
        {
            ["text"] = "文本",
            ["fontSize"] = "14",
            ["fontWeight"] = "Normal",
            ["foreground"] = "#0F172A",
            ["background"] = "Transparent",
            ["textAlign"] = "Left",
            ["verticalAlign"] = "Center",
            ["padding"] = "4",
        },
        ["rectangle"] = new()
        {
            ["fill"] = "#3B82F6",
            ["stroke"] = "#1E40AF",
            ["strokeThickness"] = "1",
            ["cornerRadius"] = "0",
            ["opacity"] = "1",
        },
        ["ellipse"] = new()
        {
            ["fill"] = "#10B981",
            ["stroke"] = "#065F46",
            ["strokeThickness"] = "1",
            ["opacity"] = "1",
        },
        ["line"] = new()
        {
            ["stroke"] = "#1F2937",
            ["strokeThickness"] = "2",
            ["strokeDashArray"] = "",
            ["x1"] = "0",
            ["y1"] = "0",
            ["x2"] = "1",
            ["y2"] = "1",
        },
        ["polyline"] = new()
        {
            ["points"] = "0,0 60,40 120,0",
            ["stroke"] = "#1F2937",
            ["strokeThickness"] = "2",
            ["strokeDashArray"] = "",
            ["opacity"] = "1",
        },
        ["polygon"] = new()
        {
            ["points"] = "60,0 120,60 60,120 0,60",
            ["fill"] = "#F59E0B",
            ["stroke"] = "#92400E",
            ["strokeThickness"] = "1",
            ["strokeDashArray"] = "",
            ["opacity"] = "1",
        },
        ["graphic-view"] = new()
        {
            ["source"] = "",
            ["stretch"] = "Uniform",
            ["opacity"] = "1",
        },
        ["io-numeric"] = new()
        {
            ["mode"] = "Output",
            ["variable"] = "",
            ["format"] = "0.##",
            ["decimals"] = "2",
            ["unit"] = "",
            ["minValue"] = "",
            ["maxValue"] = "",
            ["textAlign"] = "Right",
            ["background"] = "#FFFFFF",
            ["foreground"] = "#0F172A",
        },
        ["io-symbolic"] = new()
        {
            ["mode"] = "Output",
            ["variable"] = "",
            ["entries"] = "0=停止;1=运行",
            ["background"] = "#FFFFFF",
            ["foreground"] = "#0F172A",
        },
        ["io-graphic"] = new()
        {
            ["mode"] = "Output",
            ["variable"] = "",
            ["entries"] = "",
            ["stretch"] = "Uniform",
        },
        ["datetime"] = new()
        {
            ["mode"] = "SystemTime",
            ["variable"] = "",
            ["format"] = "yyyy-MM-dd HH:mm:ss",
            ["background"] = "#FFFFFF",
            ["foreground"] = "#0F172A",
        },

        // 元素
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
        "text"         => 160,
        "button"       => 120,
        "rectangle"    => 120,
        "ellipse"      => 80,
        "line"         => 120,
        "polyline"     => 120,
        "polygon"      => 120,
        "graphic-view" => 120,
        "io-numeric"   => 120,
        "io-symbolic"  => 120,
        "io-graphic"   => 80,
        "datetime"     => 160,
        _ => 120,
    };

    private static double GetDefaultHeight(string typeId) => typeId.ToLowerInvariant() switch
    {
        "ellipse"      => 80,
        "polyline"     => 80,
        "polygon"      => 120,
        "graphic-view" => 80,
        "io-graphic"   => 80,
        _ => 40,
    };
}
