using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ApexHMI.Models.RuntimeUi;

/// <summary>
/// 设计器画布的渲染项：包含 WidgetInstance 模型 + 预创建的 WPF 视图。
/// View 设为可观察属性，当 widget 属性修改后由 DesignerEditorViewModel
/// 重建并替换，触发 ContentPresenter 重新渲染。
/// </summary>
public sealed partial class DesignerWidgetItem : ObservableObject
{
    public DesignerWidgetItem(WidgetInstance model, FrameworkElement view)
    {
        Model = model;
        _view = view;
    }

    public WidgetInstance Model { get; }

    [ObservableProperty]
    private FrameworkElement _view;

    /// <summary>是否在多选集合中（控制画布上选中边框显示）。</summary>
    [ObservableProperty]
    private bool _isSelected;
}
