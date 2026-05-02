using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using ApexHMI.Models.RuntimeUi;
using ApexHMI.ViewModels.Runtime;
using ApexHMI.Views.Runtime.Widgets;

namespace ApexHMI.Services.RuntimeUi;

/// <summary>
/// 控件注册表：TypeId → 工厂委托。
/// 内置首批控件；后续可通过 Register 扩展。
/// </summary>
public class WidgetRegistry : IWidgetViewFactory
{
    private readonly Dictionary<string, Func<WidgetInstance, IWidgetDataContext, FrameworkElement>> _factories = new(StringComparer.OrdinalIgnoreCase);

    public WidgetRegistry()
    {
        Register("text",            (m, ctx) => CreateView(new TextWidgetViewModel(m, ctx),            new TextWidget()));
        Register("bool-lamp",       (m, ctx) => CreateView(new BoolLampWidgetViewModel(m, ctx),       new BoolLampWidget()));
        Register("numeric-readonly",(m, ctx) => CreateView(new NumericReadonlyWidgetViewModel(m, ctx), new NumericReadonlyWidget()));
        Register("button",          (m, ctx) => CreateView(new ButtonWidgetViewModel(m, ctx),          new ButtonWidget()));

        // 工业状态控件（motor/cylinder/axis/robot/stopper/alarm-banner 共用 StatusWidget）
        Register("motor",        (m, ctx) => CreateView(new StatusWidgetViewModel(m, ctx), new StatusWidget()));
        Register("cylinder",     (m, ctx) => CreateView(new StatusWidgetViewModel(m, ctx), new StatusWidget()));
        Register("axis",         (m, ctx) => CreateView(new StatusWidgetViewModel(m, ctx), new StatusWidget()));
        Register("robot",        (m, ctx) => CreateView(new StatusWidgetViewModel(m, ctx), new StatusWidget()));
        Register("stopper",      (m, ctx) => CreateView(new StatusWidgetViewModel(m, ctx), new StatusWidget()));
        Register("alarm-banner", (m, ctx) => CreateView(new StatusWidgetViewModel(m, ctx), new StatusWidget()));

        // page-button 复用 ButtonWidget
        Register("page-button",  (m, ctx) => CreateView(new ButtonWidgetViewModel(m, ctx), new ButtonWidget()));
    }

    public void Register(string typeId, Func<WidgetInstance, IWidgetDataContext, FrameworkElement> factory)
        => _factories[typeId] = factory;

    public FrameworkElement Create(WidgetInstance model, IWidgetDataContext dataContext)
    {
        if (_factories.TryGetValue(model.TypeId, out var factory))
        {
            return factory(model, dataContext);
        }

        return CreateFallback(model);
    }

    private static FrameworkElement CreateView<TViewModel>(TViewModel vm, FrameworkElement view) where TViewModel : class
    {
        view.DataContext = vm;
        return view;
    }

    private static FrameworkElement CreateFallback(WidgetInstance model)
    {
        return new Border
        {
            Width = model.Width,
            Height = model.Height,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Child = new TextBlock
            {
                Text = $"[{model.TypeId}]",
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = System.Windows.Media.Brushes.Gray
            }
        };
    }
}
