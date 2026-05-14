using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using ApexHMI.Models.RuntimeUi;
using ApexHMI.Services.RuntimeUi;

namespace ApexHMI.ViewModels.Runtime;

/// <summary>
/// 动态页面宿主 ViewModel。
/// 持有当前页面定义，并根据 PageDefinition 驱动 DynamicPageHost 渲染控件树。
/// 同时实现 IWidgetDataContext，为各 Widget VM 提供值回调注册和动作执行入口。
/// </summary>
public partial class DynamicPageHostViewModel : ObservableObject, IWidgetDataContext
{
    private readonly IWidgetViewFactory _widgetFactory;
    private readonly Action<string, string> _executeActionHandler;
    private readonly Dictionary<string, List<Action<string>>> _valueCallbacks = new(StringComparer.OrdinalIgnoreCase);
    // M3.1: 带质量的回调列表（与上方表并存；PushTagValue / PushTagValueWithQuality 各自分发）
    private readonly Dictionary<string, List<Action<string, TagQuality>>> _qualityCallbacks = new(StringComparer.OrdinalIgnoreCase);

    [ObservableProperty]
    private PageDefinition? _currentPage;

    public DynamicPageHostViewModel(
        IWidgetViewFactory widgetFactory,
        Action<string, string> executeActionHandler,
        object? shell = null)
    {
        _widgetFactory = widgetFactory;
        _executeActionHandler = executeActionHandler;
        Shell = shell;
    }

    /// <summary>暴露 Shell 给业务 widget（如 manual-cylinder-block 需要查 ManualCylinderBlockCards）。</summary>
    public object? Shell { get; }

    /// <summary>P7B: 顶层运行时无 Faceplate 上下文（faceplate 内部 widget 走 FaceplateChildDataContext）。</summary>
    public IReadOnlyDictionary<string, string>? CurrentFaceplateProperties => null;

    /// <summary>顶部页面标签栏数据源；调用 SetAvailablePages 同步。</summary>
    public ObservableCollection<PageDefinition> AvailablePages { get; } = new();

    /// <summary>是否显示顶部页面标签栏（>=2 个页面时显示）。</summary>
    public bool ShowPageTabs => AvailablePages.Count >= 2;

    public void SetAvailablePages(IEnumerable<PageDefinition> pages)
    {
        AvailablePages.Clear();
        foreach (var p in pages) AvailablePages.Add(p);
        OnPropertyChanged(nameof(ShowPageTabs));
    }

    /// <summary>页签点击 → 通过外部 handler 加载该页（由 Shell 注入）。</summary>
    public System.Action<string>? RequestLoadPage { get; set; }

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void NavigateTab(PageDefinition? page)
    {
        if (page is null) return;
        RequestLoadPage?.Invoke(page.RouteKey);
    }

    /// <summary>当前页面下渲染的所有控件元素（位置信息已在 Create 前设置）。</summary>
    public ObservableCollection<PositionedWidget> WidgetElements { get; } = new();

    /// <summary>切换到指定页面定义，重建控件树。</summary>
    public void LoadPage(PageDefinition page)
    {
        _valueCallbacks.Clear();
        _qualityCallbacks.Clear();
        WidgetElements.Clear();
        CurrentPage = page;

        // P3.1 模板：先渲染模板页控件作为底层
        if (TemplatePage is not null && !ReferenceEquals(TemplatePage, page))
        {
            foreach (var widget in TemplatePage.Widgets)
            {
                var view = _widgetFactory.Create(widget, this);
                ApexHMI.Services.RuntimeUi.AnimationEngine.Subscribe(widget, view, this);
                WidgetElements.Add(new PositionedWidget(view, widget.X, widget.Y));
            }
        }

        foreach (var widget in page.Widgets)
        {
            var view = _widgetFactory.Create(widget, this);
            // P2-V2 动画引擎：挂载新动画订阅（Appearance/Visibility/Movement）
            ApexHMI.Services.RuntimeUi.AnimationEngine.Subscribe(widget, view, this);
            WidgetElements.Add(new PositionedWidget(view, widget.X, widget.Y));
        }
    }

    /// <summary>P3.1 模板页：由外部（Shell）注入，每次 LoadPage 时叠加渲染。</summary>
    public PageDefinition? TemplatePage { get; set; }

    /// <summary>将某 Tag 最新值推送给所有关注它的 Widget。</summary>
    public void PushTagValue(string tagId, string value)
        => PushTagValueWithQuality(tagId, value, TagQuality.Good);

    /// <summary>M3.1: 推送带 quality 的 Tag 值。两种回调都通知。</summary>
    public void PushTagValueWithQuality(string tagId, string value, TagQuality quality)
    {
        if (_valueCallbacks.TryGetValue(tagId, out var callbacks))
        {
            foreach (var cb in callbacks)
            {
                Application.Current?.Dispatcher.Invoke(() => cb(value));
            }
        }
        if (_qualityCallbacks.TryGetValue(tagId, out var qcb))
        {
            foreach (var cb in qcb)
            {
                Application.Current?.Dispatcher.Invoke(() => cb(value, quality));
            }
        }
    }

    // IWidgetDataContext
    public void RegisterValueCallback(string tagId, Action<string> callback)
    {
        if (!_valueCallbacks.TryGetValue(tagId, out var list))
        {
            list = new List<Action<string>>();
            _valueCallbacks[tagId] = list;
        }
        list.Add(callback);
    }

    /// <summary>M3.1: 注册带 quality 回调。</summary>
    public void RegisterValueCallback(string tagId, Action<string, TagQuality> callback)
    {
        if (!_qualityCallbacks.TryGetValue(tagId, out var list))
        {
            list = new List<Action<string, TagQuality>>();
            _qualityCallbacks[tagId] = list;
        }
        list.Add(callback);
    }

    public void ExecuteAction(string actionType, string actionParam)
        => _executeActionHandler?.Invoke(actionType, actionParam);

    /// <summary>当前页面中所有绑定了 Tag 的 TagId 集合，用于订阅 OPC UA。</summary>
    public IEnumerable<string> BoundTagIds
    {
        get
        {
            // M3.1: 合并两种回调表的 key
            foreach (var k in _valueCallbacks.Keys) yield return k;
            foreach (var k in _qualityCallbacks.Keys)
            {
                if (!_valueCallbacks.ContainsKey(k)) yield return k;
            }
        }
    }
}

/// <summary>携带绝对坐标的控件包装，供 Canvas 布局使用。</summary>
public class PositionedWidget
{
    public PositionedWidget(FrameworkElement view, double x, double y)
    {
        View = view;
        X = x;
        Y = y;
    }

    public FrameworkElement View { get; }
    public double X { get; }
    public double Y { get; }
}
