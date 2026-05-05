using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ApexHMI.Models.RuntimeUi;

/// <summary>页面定义：唯一 ID + 标题 + 控件实例列表。</summary>
public partial class PageDefinition : ObservableObject
{
    [ObservableProperty]
    private string _id = Guid.NewGuid().ToString("N");

    /// <summary>页面显示标题。</summary>
    [ObservableProperty]
    private string _title = "新页面";

    /// <summary>页面路由键，用于页面跳转时的目标引用（如 "main" / "manual"）。</summary>
    [ObservableProperty]
    private string _routeKey = string.Empty;

    /// <summary>访问此页面所需的最低角色（null = 所有人可见）。</summary>
    [ObservableProperty]
    private string? _requiredRole;

    [ObservableProperty]
    private double _canvasWidth = 1280;

    [ObservableProperty]
    private double _canvasHeight = 720;

    /// <summary>控件实例列表，绝对定位（X/Y）。</summary>
    public List<WidgetInstance> Widgets { get; set; } = new();

    /// <summary>是否在主导航顶栏显示该页面的入口按钮。</summary>
    [ObservableProperty]
    private bool _showInTopNav;

    /// <summary>主导航按钮的 Material Design 图标名（如 "ViewDashboard" / "Cog"）。</summary>
    [ObservableProperty]
    private string? _navIcon;

    /// <summary>主导航按钮排序值（越小越靠前）。</summary>
    [ObservableProperty]
    private int _navOrder;
}
