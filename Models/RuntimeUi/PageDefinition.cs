using System;
using System.Collections.Generic;

namespace ApexHMI.Models.RuntimeUi;

/// <summary>页面定义：唯一 ID + 标题 + 控件实例列表。</summary>
public class PageDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>页面显示标题。</summary>
    public string Title { get; set; } = "新页面";

    /// <summary>页面路由键，用于页面跳转时的目标引用（如 "main" / "manual"）。</summary>
    public string RouteKey { get; set; } = string.Empty;

    /// <summary>访问此页面所需的最低角色（null = 所有人可见）。</summary>
    public string? RequiredRole { get; set; }

    public double CanvasWidth { get; set; } = 1280;
    public double CanvasHeight { get; set; } = 720;

    /// <summary>控件实例列表，绝对定位（X/Y）。</summary>
    public List<WidgetInstance> Widgets { get; set; } = new();
}
