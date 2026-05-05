using System;
using System.Windows;
using ApexHMI.Models.RuntimeUi;

namespace ApexHMI.Services.RuntimeUi;

/// <summary>
/// Widget 数据上下文：提供值回调注册、动作执行入口、以及业务 widget
/// 访问 Shell 的钩子（如 manual-cylinder-block 需要从 Shell 取气缸列表）。
/// 由 DynamicPageHostViewModel 与 DesignModeWidgetDataContext 实现。
/// </summary>
public interface IWidgetDataContext
{
    void RegisterValueCallback(string tagId, Action<string> callback);
    void ExecuteAction(string actionType, string actionParam);

    /// <summary>
    /// 业务 widget 访问 Shell（MainViewModel）的入口。
    /// 设计模式与运行模式都返回同一个 Shell，让画布上业务控件能显示真实数据。
    /// </summary>
    object? Shell { get; }
}

/// <summary>控件视图工厂接口：根据 WidgetInstance 创建 WPF FrameworkElement。</summary>
public interface IWidgetViewFactory
{
    FrameworkElement Create(WidgetInstance model, IWidgetDataContext dataContext);
    void Register(string typeId, Func<WidgetInstance, IWidgetDataContext, FrameworkElement> factory);
}
