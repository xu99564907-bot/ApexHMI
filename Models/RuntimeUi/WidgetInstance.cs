using System;
using System.Collections.Generic;

namespace ApexHMI.Models.RuntimeUi;

/// <summary>运行时控件实例：类型 + 位置 + 属性字典 + 数据绑定。</summary>
public class WidgetInstance
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>控件类型 ID，对应 WidgetRegistry 中注册的 key，如 "text"/"bool-lamp"/"numeric-readonly"/"button"。</summary>
    public string TypeId { get; set; } = "text";

    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 120;
    public double Height { get; set; } = 40;

    /// <summary>控件属性键值对。各类型控件自行约定 key 名称。</summary>
    public Dictionary<string, string> Properties { get; set; } = new();

    /// <summary>数据绑定，可为 null 表示无 OPC UA 绑定（纯静态控件）。</summary>
    public BindingSpec? Binding { get; set; }

    /// <summary>点击/写值动作类型：write-bool / write-pulse / navigate。</summary>
    public string? ActionType { get; set; }

    /// <summary>动作参数：write 时为写入值；navigate 时为目标页面 RouteKey。</summary>
    public string? ActionParam { get; set; }
}
