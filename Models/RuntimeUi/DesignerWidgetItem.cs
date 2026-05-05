using System.Windows;

namespace ApexHMI.Models.RuntimeUi;

/// <summary>
/// 设计器画布的渲染项：包含 WidgetInstance 模型 + 预创建的 WPF 视图。
/// 通过预创建避免在 XAML 渲染路径上动态调用 IWidgetViewFactory 引发的反复重建/卡死。
/// </summary>
public sealed class DesignerWidgetItem
{
    public DesignerWidgetItem(WidgetInstance model, FrameworkElement view)
    {
        Model = model;
        View = view;
    }

    public WidgetInstance Model { get; }
    public FrameworkElement View { get; }
}
