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
        // 基本对象（P3）
        Register("text",         (m, ctx) => CreateView(new TextWidgetViewModel(m, ctx),         new TextWidget()));
        Register("rectangle",    (m, ctx) => CreateView(new RectangleWidgetViewModel(m, ctx),    new RectangleWidget()));
        Register("ellipse",      (m, ctx) => CreateView(new EllipseWidgetViewModel(m, ctx),      new EllipseWidget()));
        Register("line",         (m, ctx) => CreateView(new LineWidgetViewModel(m, ctx),         new LineWidget()));
        Register("polyline",     (m, ctx) => CreateView(new PolylineWidgetViewModel(m, ctx),     new PolylineWidget()));
        Register("polygon",      (m, ctx) => CreateView(new PolygonWidgetViewModel(m, ctx),      new PolygonWidget()));
        Register("graphic-view", (m, ctx) => CreateView(new GraphicViewWidgetViewModel(m, ctx),  new GraphicViewWidget()));
        Register("io-numeric",   (m, ctx) => CreateView(new IoNumericWidgetViewModel(m, ctx),    new IoNumericWidget()));
        Register("io-symbolic",  (m, ctx) => CreateView(new IoSymbolicWidgetViewModel(m, ctx),   new IoSymbolicWidget()));
        Register("io-graphic",   (m, ctx) => CreateView(new IoGraphicWidgetViewModel(m, ctx),    new IoGraphicWidget()));
        Register("datetime",     (m, ctx) => CreateView(new DateTimeWidgetViewModel(m, ctx),     new DateTimeWidget()));

        // 元素
        Register("button",       (m, ctx) => CreateView(new ButtonWidgetViewModel(m, ctx),       new ButtonWidget()));
        Register("round-button", (m, ctx) => CreateView(new ButtonWidgetViewModel(m, ctx),       new RoundButtonWidget()));
        Register("switch",       (m, ctx) => CreateView(new SwitchWidgetViewModel(m, ctx),       new SwitchWidget()));
        Register("bar",          (m, ctx) => CreateView(new BarWidgetViewModel(m, ctx),          new BarWidget()));
        Register("gauge",        (m, ctx) => CreateView(new GaugeWidgetViewModel(m, ctx),        new GaugeWidget()));
        Register("slider",       (m, ctx) => CreateView(new SliderWidgetViewModel(m, ctx),       new SliderWidget()));
        Register("scrollbar",    (m, ctx) => CreateView(new SliderWidgetViewModel(m, ctx),       new ScrollBarWidget()));
        Register("clock",        (m, ctx) => CreateView(new ClockWidgetViewModel(m, ctx),        new ClockWidget()));
        Register("combobox",     (m, ctx) => CreateView(new OptionItemsWidgetViewModel(m, ctx),  new ComboBoxWidget()));
        Register("listbox",      (m, ctx) => CreateView(new OptionItemsWidgetViewModel(m, ctx),  new ListBoxWidget()));
        Register("checkbox",     (m, ctx) => CreateView(new CheckBoxWidgetViewModel(m, ctx),     new CheckBoxWidget()));
        Register("optiongroup",  (m, ctx) => CreateView(new OptionItemsWidgetViewModel(m, ctx),  new OptionGroupWidget()));

        // P5 控件
        Register("screen-window", (m, ctx) => CreateView(new ScreenWindowWidgetViewModel(m, ctx), new ScreenWindowWidget()));
        Register("table-view",    (m, ctx) => CreateView(new TableViewWidgetViewModel(m, ctx),    new TableViewWidget()));
        Register("alarm-view",    (m, ctx) => CreateView(new AlarmViewWidgetViewModel(m, ctx),    new AlarmViewWidget()));
        Register("trend-view",    (m, ctx) => CreateView(new TrendViewWidgetViewModel(m, ctx),    new TrendViewWidget()));
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
