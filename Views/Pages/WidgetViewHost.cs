using System.Windows;
using System.Windows.Controls;
using ApexHMI.Models.RuntimeUi;
using ApexHMI.Services.RuntimeUi;

namespace ApexHMI.Views.Pages;

/// <summary>
/// 附加属性：让 XAML 中任何 ContentControl/ContentPresenter 通过 IWidgetViewFactory
/// 渲染一个 WidgetInstance 的真容（用于设计器画布的 WYSIWYG）。
///
/// 用法：
///   &lt;ContentControl
///       wph:WidgetViewHost.Factory="{Binding DataContext.WidgetViewFactory, RelativeSource=...}"
///       wph:WidgetViewHost.DataContext="{Binding DataContext.DesignModeContext, RelativeSource=...}"
///       wph:WidgetViewHost.Model="{Binding}"/&gt;
/// </summary>
public static class WidgetViewHost
{
    // ---- Factory 附加属性 ----
    public static readonly DependencyProperty FactoryProperty =
        DependencyProperty.RegisterAttached(
            "Factory",
            typeof(IWidgetViewFactory),
            typeof(WidgetViewHost),
            new PropertyMetadata(null, OnAnyChanged));

    public static IWidgetViewFactory? GetFactory(DependencyObject d) =>
        (IWidgetViewFactory?)d.GetValue(FactoryProperty);
    public static void SetFactory(DependencyObject d, IWidgetViewFactory? value) =>
        d.SetValue(FactoryProperty, value);

    // ---- DataContext (IWidgetDataContext) 附加属性 ----
    public static readonly DependencyProperty DataContextProperty =
        DependencyProperty.RegisterAttached(
            "DataContext",
            typeof(IWidgetDataContext),
            typeof(WidgetViewHost),
            new PropertyMetadata(null, OnAnyChanged));

    public static IWidgetDataContext? GetDataContext(DependencyObject d) =>
        (IWidgetDataContext?)d.GetValue(DataContextProperty);
    public static void SetDataContext(DependencyObject d, IWidgetDataContext? value) =>
        d.SetValue(DataContextProperty, value);

    // ---- Model 附加属性 ----
    public static readonly DependencyProperty ModelProperty =
        DependencyProperty.RegisterAttached(
            "Model",
            typeof(WidgetInstance),
            typeof(WidgetViewHost),
            new PropertyMetadata(null, OnAnyChanged));

    public static WidgetInstance? GetModel(DependencyObject d) =>
        (WidgetInstance?)d.GetValue(ModelProperty);
    public static void SetModel(DependencyObject d, WidgetInstance? value) =>
        d.SetValue(ModelProperty, value);

    // ---- 渲染逻辑 ----
    private static void OnAnyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var factory = GetFactory(d);
        var ctx = GetDataContext(d);
        var model = GetModel(d);

        if (factory is null || ctx is null || model is null)
            return;

        var view = factory.Create(model, ctx);
        view.IsHitTestVisible = false; // 设计模式：禁止 widget 自身响应鼠标，所有点击交给上层选中层处理

        switch (d)
        {
            case ContentControl cc:
                cc.Content = view;
                break;
            case ContentPresenter cp:
                cp.Content = view;
                break;
        }
    }
}
