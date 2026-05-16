using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ApexHMI.Models.RuntimeUi;

/// <summary>P2.5: 状态动画规则。Tag 值满足条件 → 设置某个 Property 为指定值。</summary>
public partial class WidgetAnimation : ObservableObject
{
    [ObservableProperty] private string _tagId = string.Empty;
    /// <summary>比较运算：eq / ne / gt / lt / gte / lte / true / false</summary>
    [ObservableProperty] private string _op = "eq";
    [ObservableProperty] private string _compareTo = string.Empty;
    /// <summary>满足条件时设置的属性 key（widget Properties 字典）。</summary>
    [ObservableProperty] private string _targetProperty = string.Empty;
    [ObservableProperty] private string _targetValue = string.Empty;
}

/// <summary>运行时控件实例：类型 + 位置 + 属性字典 + 数据绑定。</summary>
public partial class WidgetInstance : ObservableObject
{
    [ObservableProperty]
    private string _id = Guid.NewGuid().ToString("N");

    /// <summary>控件类型 ID，对应 WidgetRegistry 中注册的 key，如 "text"/"bool-lamp"/"numeric-readonly"/"button"。</summary>
    [ObservableProperty]
    private string _typeId = "text";

    [ObservableProperty]
    private double _x;

    [ObservableProperty]
    private double _y;

    [ObservableProperty]
    private double _width = 120;

    [ObservableProperty]
    private double _height = 40;

    /// <summary>控件属性键值对。各类型控件自行约定 key 名称。</summary>
    [ObservableProperty]
    private Dictionary<string, string> _properties = new();

    /// <summary>数据绑定，可为 null 表示无 OPC UA 绑定（纯静态控件）。</summary>
    [ObservableProperty]
    private BindingSpec? _binding;

    /// <summary>点击/写值动作类型：write-bool / write-pulse / navigate。</summary>
    [ObservableProperty]
    private string? _actionType;

    /// <summary>动作参数：write 时为写入值；navigate 时为目标页面 RouteKey。</summary>
    [ObservableProperty]
    private string? _actionParam;

    /// <summary>P3.3: 控件级所需角色（null/空=所有人可见、可点）。</summary>
    [ObservableProperty]
    private string? _requiredRole;

    /// <summary>P7: Faceplate 实例化时使用的 Faceplate 版本号（仅当 TypeId 以 <c>faceplate:</c> 开头时有意义）。
    /// <para>用于检测原 Faceplate 升级后实例是否需要同步。</para></summary>
    [ObservableProperty]
    private string? _faceplateVersion;

    /// <summary>P2.5: 状态动画规则（Tag 值驱动属性变化）。
    /// <para>P2-V2 之后保留为旧字段，迁移到 <see cref="Appearance"/> / <see cref="Visibility"/> / <see cref="Movement"/>。
    /// 新模型为空时运行时仍 fallback 使用本列表（向后兼容）。</para></summary>
    public List<WidgetAnimation> Animations { get; set; } = new();

    /// <summary>P2-V2: 外观动画（颜色 / 闪烁，单 widget 单个）。</summary>
    public AppearanceAnimation? Appearance { get; set; }

    /// <summary>P2-V2: 可见性动画（True/False/Range → 显示/隐藏/禁用）。</summary>
    public VisibilityAnimation? Visibility { get; set; }

    /// <summary>P2-V2: 移动动画（水平 / 垂直 / 直接 / 对角线，四选一）。</summary>
    public MoveAnimation? Movement { get; set; }

    /// <summary>P1: 事件名 → 该事件下顺序执行的动作步骤列表。
    /// <para>事件名约定：click / press / release / activate / deactivate / valueChanged</para>
    /// <para>旧字段 ActionType+ActionParam 保留，加载时自动迁移到 Events["click"]。</para>
    /// </summary>
    public Dictionary<string, List<ActionStep>> Events { get; set; } = new();

    /// <summary>通知 Properties 字典内容已变更（用于编辑器属性修改后即时刷新）。</summary>
    public void NotifyPropertiesChanged()
    {
        OnPropertyChanged(nameof(Properties));
    }
}
