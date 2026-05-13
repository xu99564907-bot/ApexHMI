using System;
using System.Collections.Generic;
using ApexHMI.Models.RuntimeUi;
using Serilog;

namespace ApexHMI.Services.RuntimeUi;

/// <summary>控件编辑器服务实现。
/// <para>P10A: 旧 DefaultProperties 硬编码表已移除（212 行），默认值统一改由
/// <see cref="WidgetSchemaCatalog"/> 提供，schema 是唯一真值来源。</para>
/// </summary>
public sealed class WidgetEditorService : IWidgetEditorService
{
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

        // P10A: schema 是唯一默认值来源
        var schema = WidgetSchemaCatalog.Lookup(typeId);
        if (schema is not null)
        {
            foreach (var desc in schema.Properties)
            {
                if (!widget.Properties.ContainsKey(desc.Key) && !string.IsNullOrEmpty(desc.DefaultValue))
                {
                    widget.Properties[desc.Key] = desc.DefaultValue;
                }
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
        "round-button" => 80,
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
        "switch"       => 100,
        "bar"          => 60,
        "gauge"        => 160,
        "slider"       => 200,
        "scrollbar"    => 200,
        "clock"        => 180,
        "combobox"     => 160,
        "listbox"      => 160,
        "checkbox"     => 120,
        "optiongroup"  => 160,
        _ => 120,
    };

    private static double GetDefaultHeight(string typeId) => typeId.ToLowerInvariant() switch
    {
        "ellipse"      => 80,
        "polyline"     => 80,
        "polygon"      => 120,
        "graphic-view" => 80,
        "io-graphic"   => 80,
        "round-button" => 80,
        "bar"          => 160,
        "gauge"        => 140,
        "listbox"      => 120,
        "optiongroup"  => 120,
        "clock"        => 36,
        _ => 40,
    };
}
