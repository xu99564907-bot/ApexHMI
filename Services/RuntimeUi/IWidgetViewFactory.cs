using System;
using System.Windows;
using ApexHMI.Models.RuntimeUi;

namespace ApexHMI.Services.RuntimeUi;

/// <summary>
/// Widget 数据上下文：提供值回调注册和动作执行入口。
/// 由 DynamicPageHostViewModel 实现。
/// </summary>
public interface IWidgetDataContext
{
    void RegisterValueCallback(string tagId, Action<string> callback);
    void ExecuteAction(string actionType, string actionParam);
}

/// <summary>控件视图工厂接口：根据 WidgetInstance 创建 WPF FrameworkElement。</summary>
public interface IWidgetViewFactory
{
    FrameworkElement Create(WidgetInstance model, IWidgetDataContext dataContext);
    void Register(string typeId, Func<WidgetInstance, IWidgetDataContext, FrameworkElement> factory);
}
