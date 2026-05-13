using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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

        // P8A 配方视图
        Register("recipe-view",   (m, ctx) => CreateView(new RecipeViewWidgetViewModel(m, ctx),   new RecipeViewWidget()));

        // P8B 用户视图
        Register("user-view",     (m, ctx) => CreateView(new UserViewWidgetViewModel(m, ctx),     new UserViewWidget()));
    }

    public void Register(string typeId, Func<WidgetInstance, IWidgetDataContext, FrameworkElement> factory)
        => _factories[typeId] = factory;

    public FrameworkElement Create(WidgetInstance model, IWidgetDataContext dataContext)
    {
        // P7B: faceplate:<id> 前缀 → 渲染 Faceplate 实例
        if (model.TypeId is { Length: > 10 } tid && tid.StartsWith("faceplate:", StringComparison.OrdinalIgnoreCase))
        {
            return CreateFaceplateInstance(model, dataContext, tid.Substring("faceplate:".Length));
        }

        if (_factories.TryGetValue(model.TypeId, out var factory))
        {
            return factory(model, dataContext);
        }

        return CreateFallback(model);
    }

    // ===== P7B: Faceplate 渲染 =====

    [ThreadStatic] private static HashSet<string>? _renderStack;

    private FrameworkElement CreateFaceplateInstance(WidgetInstance model, IWidgetDataContext dataContext, string faceplateId)
    {
        var lib = DesignerContext.Document?.Faceplates;
        var fp = lib?.Faceplates.FirstOrDefault(f => string.Equals(f.Id, faceplateId, StringComparison.Ordinal));
        if (fp is null)
        {
            return CreatePlaceholder(model, $"[未知 Faceplate: {faceplateId}]", Brushes.Crimson);
        }

        // 嵌套深度 / 循环检测
        _renderStack ??= new HashSet<string>(StringComparer.Ordinal);
        if (_renderStack.Count >= 5 || _renderStack.Contains(faceplateId))
        {
            return CreatePlaceholder(model, "[Faceplate 嵌套过深或循环]", Brushes.OrangeRed);
        }

        _renderStack.Add(faceplateId);
        try
        {
            // 合并接口属性默认值与实例覆盖值
            var propValues = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var def in fp.InterfaceProperties)
            {
                propValues[def.Key] = def.DefaultValue ?? string.Empty;
            }
            foreach (var kv in model.Properties)
            {
                propValues[kv.Key] = kv.Value;
            }

            var childCtx = new FaceplateChildDataContext(dataContext, propValues);
            var canvas = new Canvas
            {
                Width = model.Width,
                Height = model.Height,
                Background = Brushes.Transparent,
                ClipToBounds = true,
            };

            // 根据 Faceplate.DefaultWidth/Height 与实例 Width/Height 之比缩放内部坐标
            double sx = fp.DefaultWidth > 0 ? model.Width / fp.DefaultWidth : 1.0;
            double sy = fp.DefaultHeight > 0 ? model.Height / fp.DefaultHeight : 1.0;
            if (Math.Abs(sx - 1.0) > 0.001 || Math.Abs(sy - 1.0) > 0.001)
            {
                canvas.LayoutTransform = new ScaleTransform(sx, sy);
                canvas.Width = fp.DefaultWidth;
                canvas.Height = fp.DefaultHeight;
            }

            foreach (var inner in fp.InnerScreen.Widgets)
            {
                var innerView = Create(inner, childCtx);
                innerView.Width = inner.Width;
                innerView.Height = inner.Height;
                Canvas.SetLeft(innerView, inner.X);
                Canvas.SetTop(innerView, inner.Y);
                canvas.Children.Add(innerView);
            }

            return canvas;
        }
        finally
        {
            _renderStack.Remove(faceplateId);
        }
    }

    private static FrameworkElement CreatePlaceholder(WidgetInstance model, string text, Brush border)
    {
        return new Border
        {
            Width = model.Width,
            Height = model.Height,
            BorderBrush = border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Background = new SolidColorBrush(Color.FromArgb(20, 200, 50, 50)),
            Child = new TextBlock
            {
                Text = text,
                Foreground = border,
                FontSize = 11,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
            }
        };
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
