#nullable enable
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ApexHMI.Models.RuntimeUi;

// =====================================================================
// P2 动画系统 V2 — 三大类动画
// =====================================================================
// 设计要点：
// - 数据模型独立可序列化，绑到 WidgetInstance.Appearance / Visibility / Movement。
// - 运行时由 Services/RuntimeUi/AnimationEngine 订阅 PLC 值变化驱动 view 视觉变化。
// - 与旧 WidgetAnimation 共存：新字段空时 fallback 走旧列表（向后兼容）。
// =====================================================================

/// <summary>外观动画匹配模式。</summary>
public enum AppearanceMatchType
{
    /// <summary>变量值落在 [RangeFrom, RangeTo] 区间时匹配。</summary>
    Range,
    /// <summary>变量值与 BitMask 按位与非 0 时匹配（多位）。</summary>
    MultiBit,
    /// <summary>变量值的第 BitIndex 位为 1 时匹配。</summary>
    SingleBit,
}

/// <summary>外观动画一行规则。</summary>
public partial class AppearanceRow : ObservableObject
{
    /// <summary>Range 模式：起始值。</summary>
    [ObservableProperty] private string _rangeFrom = string.Empty;
    /// <summary>Range 模式：结束值。</summary>
    [ObservableProperty] private string _rangeTo = string.Empty;
    /// <summary>SingleBit 模式：位号（0..31）。</summary>
    [ObservableProperty] private int _bitIndex;
    /// <summary>MultiBit 模式：位掩码（支持 0x... hex 或 十进制）。</summary>
    [ObservableProperty] private string _bitMask = string.Empty;
    /// <summary>背景色（hex/资源名，空=不改）。</summary>
    [ObservableProperty] private string _background = string.Empty;
    /// <summary>前景色（hex/资源名，空=不改）。</summary>
    [ObservableProperty] private string _foreground = string.Empty;
    /// <summary>是否闪烁。</summary>
    [ObservableProperty] private bool _blink;
}

/// <summary>外观动画：基于变量值匹配，控制控件颜色 / 闪烁。</summary>
public partial class AppearanceAnimation : ObservableObject
{
    [ObservableProperty] private string _tagId = string.Empty;
    [ObservableProperty] private AppearanceMatchType _matchType = AppearanceMatchType.Range;

    /// <summary>规则行（按列表顺序匹配，先匹中先生效）。</summary>
    public List<AppearanceRow> Rows { get; set; } = new();
}

/// <summary>可见性动画触发模式。</summary>
public enum VisibilityMode
{
    /// <summary>变量为 True 时显示。</summary>
    WhenTrue,
    /// <summary>变量为 False 时显示。</summary>
    WhenFalse,
    /// <summary>变量值在 [RangeFrom, RangeTo] 区间内时显示。</summary>
    WhenInRange,
}

/// <summary>条件不满足时的处理方式。</summary>
public enum VisibilityOtherwise
{
    /// <summary>隐藏（Collapsed）。</summary>
    Hidden,
    /// <summary>禁用（仍可见但 IsEnabled=false）。</summary>
    Disabled,
}

/// <summary>可见性动画。</summary>
public partial class VisibilityAnimation : ObservableObject
{
    [ObservableProperty] private string _tagId = string.Empty;
    [ObservableProperty] private VisibilityMode _mode = VisibilityMode.WhenTrue;
    [ObservableProperty] private string _rangeFrom = string.Empty;
    [ObservableProperty] private string _rangeTo = string.Empty;
    [ObservableProperty] private VisibilityOtherwise _otherwise = VisibilityOtherwise.Hidden;
}

/// <summary>移动动画类型。</summary>
public enum MoveType
{
    /// <summary>水平：单变量驱动 X。</summary>
    Horizontal,
    /// <summary>垂直：单变量驱动 Y。</summary>
    Vertical,
    /// <summary>直接：两个变量分别等于像素 X/Y（坐标显示）。</summary>
    Direct,
    /// <summary>对角线：两个变量分别映射 X/Y。</summary>
    Diagonal,
}

/// <summary>移动动画。</summary>
public partial class MoveAnimation : ObservableObject
{
    [ObservableProperty] private MoveType _moveType = MoveType.Horizontal;

    [ObservableProperty] private string _tagIdX = string.Empty;
    [ObservableProperty] private string _tagIdY = string.Empty;

    [ObservableProperty] private double _rangeMinX = 0;
    [ObservableProperty] private double _rangeMaxX = 100;
    [ObservableProperty] private double _pixelStartX = 0;
    [ObservableProperty] private double _pixelEndX = 200;

    [ObservableProperty] private double _rangeMinY = 0;
    [ObservableProperty] private double _rangeMaxY = 100;
    [ObservableProperty] private double _pixelStartY = 0;
    [ObservableProperty] private double _pixelEndY = 200;
}
