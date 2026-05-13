#nullable enable
using CommunityToolkit.Mvvm.ComponentModel;

namespace ApexHMI.ViewModels.Modules;

/// <summary>P2-V2 动画 Tab 左导航一项：分类 ID + 显示名 + 启用状态 + 选中状态。</summary>
public partial class AnimationCategoryItem : ObservableObject
{
    /// <summary>分类 ID：overview / appearance / visibility / move-horizontal / move-vertical / move-direct / move-diagonal。</summary>
    public string CategoryId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>组标题（用于分组渲染：总览 / 显示 / 移动）。</summary>
    public string Group { get; init; } = string.Empty;

    /// <summary>当前 widget 是否已配置该动画。</summary>
    [ObservableProperty] private bool _isEnabled;

    /// <summary>是否选中（左导航高亮）。</summary>
    [ObservableProperty] private bool _isSelected;

    /// <summary>简短说明（总览面板展示已绑变量名）。</summary>
    [ObservableProperty] private string _summary = string.Empty;
}
