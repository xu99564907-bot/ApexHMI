#nullable enable
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ApexHMI.Models.RuntimeUi;

namespace ApexHMI.ViewModels.Modules;

/// <summary>
/// P2-V2: 动画 Tab 状态与命令。分类导航 / 启用-停用 / 跳转。
/// 实际编辑数据直接绑到 SelectedWidget.Appearance/Visibility/Movement（ObservableObject，UI 双向）。
/// </summary>
public partial class DesignerEditorViewModel
{
    /// <summary>动画 Tab 左导航 7 项。</summary>
    public ObservableCollection<AnimationCategoryItem> AnimationCategories { get; } = new()
    {
        new() { CategoryId = "overview",       DisplayName = "总览",       Group = "总览" },
        new() { CategoryId = "appearance",     DisplayName = "外观",       Group = "显示" },
        new() { CategoryId = "visibility",     DisplayName = "可见性",     Group = "显示" },
        new() { CategoryId = "move-horizontal",DisplayName = "水平移动",   Group = "移动" },
        new() { CategoryId = "move-vertical",  DisplayName = "垂直移动",   Group = "移动" },
        new() { CategoryId = "move-direct",    DisplayName = "直接移动",   Group = "移动" },
        new() { CategoryId = "move-diagonal",  DisplayName = "对角线移动", Group = "移动" },
    };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOverviewSelected))]
    [NotifyPropertyChangedFor(nameof(IsAppearanceSelected))]
    [NotifyPropertyChangedFor(nameof(IsVisibilitySelected))]
    [NotifyPropertyChangedFor(nameof(IsHorizontalSelected))]
    [NotifyPropertyChangedFor(nameof(IsVerticalSelected))]
    [NotifyPropertyChangedFor(nameof(IsDirectSelected))]
    [NotifyPropertyChangedFor(nameof(IsDiagonalSelected))]
    private string _selectedAnimationCategory = "overview";

    public bool IsOverviewSelected   => SelectedAnimationCategory == "overview";
    public bool IsAppearanceSelected => SelectedAnimationCategory == "appearance";
    public bool IsVisibilitySelected => SelectedAnimationCategory == "visibility";
    public bool IsHorizontalSelected => SelectedAnimationCategory == "move-horizontal";
    public bool IsVerticalSelected   => SelectedAnimationCategory == "move-vertical";
    public bool IsDirectSelected     => SelectedAnimationCategory == "move-direct";
    public bool IsDiagonalSelected   => SelectedAnimationCategory == "move-diagonal";

    partial void OnSelectedAnimationCategoryChanged(string value)
    {
        foreach (var c in AnimationCategories) c.IsSelected = c.CategoryId == value;
    }

    /// <summary>当 SelectedWidget 切换时刷新动画分类启用状态 + 总览汇总。</summary>
    private void RefreshAnimationCategoryStates()
    {
        var w = SelectedWidget;
        foreach (var c in AnimationCategories)
        {
            c.IsSelected = c.CategoryId == SelectedAnimationCategory;
            switch (c.CategoryId)
            {
                case "appearance":
                    c.IsEnabled = w?.Appearance is not null;
                    c.Summary = w?.Appearance?.TagId ?? string.Empty;
                    break;
                case "visibility":
                    c.IsEnabled = w?.Visibility is not null;
                    c.Summary = w?.Visibility?.TagId ?? string.Empty;
                    break;
                case "move-horizontal":
                    c.IsEnabled = w?.Movement is { MoveType: MoveType.Horizontal };
                    c.Summary = c.IsEnabled ? (w?.Movement?.TagIdX ?? "") : "";
                    break;
                case "move-vertical":
                    c.IsEnabled = w?.Movement is { MoveType: MoveType.Vertical };
                    c.Summary = c.IsEnabled ? (w?.Movement?.TagIdY ?? "") : "";
                    break;
                case "move-direct":
                    c.IsEnabled = w?.Movement is { MoveType: MoveType.Direct };
                    c.Summary = c.IsEnabled ? $"{w?.Movement?.TagIdX} , {w?.Movement?.TagIdY}" : "";
                    break;
                case "move-diagonal":
                    c.IsEnabled = w?.Movement is { MoveType: MoveType.Diagonal };
                    c.Summary = c.IsEnabled ? $"{w?.Movement?.TagIdX} , {w?.Movement?.TagIdY}" : "";
                    break;
            }
        }
        OnPropertyChanged(nameof(WidgetAppearance));
        OnPropertyChanged(nameof(WidgetVisibility));
        OnPropertyChanged(nameof(WidgetMovement));
    }

    /// <summary>选中分类。</summary>
    [RelayCommand]
    private void SelectAnimationCategory(string? categoryId)
    {
        if (string.IsNullOrEmpty(categoryId)) return;
        SelectedAnimationCategory = categoryId!;
    }

    /// <summary>启用某类动画（在 widget 上创建对应对象）。</summary>
    [RelayCommand]
    private void EnableAnimation(string? categoryId)
    {
        var w = SelectedWidget;
        if (w is null || string.IsNullOrEmpty(categoryId)) return;
        switch (categoryId)
        {
            case "appearance":
                w.Appearance ??= new AppearanceAnimation
                {
                    MatchType = AppearanceMatchType.Range,
                    Rows = { new AppearanceRow { RangeFrom = "0", RangeTo = "0", Background = "#94A3B8" } }
                };
                break;
            case "visibility":
                w.Visibility ??= new VisibilityAnimation();
                break;
            case "move-horizontal":
                w.Movement = new MoveAnimation { MoveType = MoveType.Horizontal };
                break;
            case "move-vertical":
                w.Movement = new MoveAnimation { MoveType = MoveType.Vertical };
                break;
            case "move-direct":
                w.Movement = new MoveAnimation { MoveType = MoveType.Direct };
                break;
            case "move-diagonal":
                w.Movement = new MoveAnimation { MoveType = MoveType.Diagonal };
                break;
        }
        MarkPageEdited();
        RefreshAnimationCategoryStates();
        SelectedAnimationCategory = categoryId!;
    }

    /// <summary>停用某类动画（删除 widget 上对应对象）。</summary>
    [RelayCommand]
    private void DisableAnimation(string? categoryId)
    {
        var w = SelectedWidget;
        if (w is null || string.IsNullOrEmpty(categoryId)) return;
        switch (categoryId)
        {
            case "appearance": w.Appearance = null; break;
            case "visibility": w.Visibility = null; break;
            case "move-horizontal":
            case "move-vertical":
            case "move-direct":
            case "move-diagonal":
                w.Movement = null;
                break;
        }
        MarkPageEdited();
        RefreshAnimationCategoryStates();
    }

    /// <summary>外观行操作：增 / 删 / 上移 / 下移。</summary>
    [RelayCommand]
    private void AddAppearanceRow()
    {
        var app = SelectedWidget?.Appearance;
        if (app is null) return;
        app.Rows.Add(new AppearanceRow { RangeFrom = "0", RangeTo = "0", Background = "#FFFFFF" });
        MarkPageEdited();
        OnPropertyChanged(nameof(WidgetAppearance));
        // 直接重建 ObservableCollection 触发 UI 刷新（List<T> 改动后 DataGrid/ItemsControl 不会自动收到通知）
        RefreshAppearanceRowsCollection();
    }

    [RelayCommand]
    private void RemoveAppearanceRow(AppearanceRow? row)
    {
        var app = SelectedWidget?.Appearance;
        if (app is null || row is null) return;
        app.Rows.Remove(row);
        MarkPageEdited();
        RefreshAppearanceRowsCollection();
    }

    [RelayCommand]
    private void MoveAppearanceRowUp(AppearanceRow? row)
    {
        var app = SelectedWidget?.Appearance;
        if (app is null || row is null) return;
        var idx = app.Rows.IndexOf(row);
        if (idx <= 0) return;
        (app.Rows[idx - 1], app.Rows[idx]) = (app.Rows[idx], app.Rows[idx - 1]);
        MarkPageEdited();
        RefreshAppearanceRowsCollection();
    }

    [RelayCommand]
    private void MoveAppearanceRowDown(AppearanceRow? row)
    {
        var app = SelectedWidget?.Appearance;
        if (app is null || row is null) return;
        var idx = app.Rows.IndexOf(row);
        if (idx < 0 || idx >= app.Rows.Count - 1) return;
        (app.Rows[idx + 1], app.Rows[idx]) = (app.Rows[idx], app.Rows[idx + 1]);
        MarkPageEdited();
        RefreshAppearanceRowsCollection();
    }

    /// <summary>外观行集合（双向绑 UI 用，背后转 SelectedWidget.Appearance.Rows）。</summary>
    public ObservableCollection<AppearanceRow> AppearanceRows { get; } = new();

    private void RefreshAppearanceRowsCollection()
    {
        AppearanceRows.Clear();
        var app = SelectedWidget?.Appearance;
        if (app is null) return;
        foreach (var r in app.Rows) AppearanceRows.Add(r);
    }

    /// <summary>直接绑到 SelectedWidget 的三个动画字段，供 XAML 双向。</summary>
    public AppearanceAnimation? WidgetAppearance => SelectedWidget?.Appearance;
    public VisibilityAnimation? WidgetVisibility => SelectedWidget?.Visibility;
    public MoveAnimation?       WidgetMovement  => SelectedWidget?.Movement;

    /// <summary>在 OnSelectedWidgetChanged 被调用，把动画 Tab 状态刷新到当前 widget。</summary>
    internal void NotifyAnimationSelectionChanged()
    {
        RefreshAnimationCategoryStates();
        RefreshAppearanceRowsCollection();
    }
}
