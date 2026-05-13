#nullable enable
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Windows;
using ApexHMI.Models.RuntimeUi;
using ApexHMI.Services.RuntimeUi;

namespace ApexHMI.ViewModels.Runtime;

/// <summary>
/// P5A 画面窗口：把另一个 PageDefinition 嵌入当前画面。
/// <para>属性：</para>
/// <list type="bullet">
///   <item><c>referencedPageRouteKey</c>：目标页面 RouteKey</item>
///   <item><c>modal</c>：true=模态弹窗（第一版仅显示占位），false=嵌入式</item>
///   <item><c>showTitle</c>：是否显示标题栏</item>
///   <item><c>title</c>：标题覆盖文本（空则用目标页 Title）</item>
/// </list>
/// 通过 IWidgetDataContext.Shell 反射访问 RuntimeProjectService.Current，递归创建子 widget views。
/// </summary>
public class ScreenWindowWidgetViewModel : WidgetViewModelBase
{
    public ScreenWindowWidgetViewModel(WidgetInstance model, IWidgetDataContext dataContext)
        : base(model, dataContext)
    {
        ResolveTargetPage();
    }

    public string ReferencedPageRouteKey => Prop("referencedPageRouteKey", "");
    public string ModalRaw  => Prop("modal", "false");
    public string ShowTitleRaw => Prop("showTitle", "true");
    public string TitleOverride => Prop("title", "");

    public bool IsModal => string.Equals(ModalRaw, "true", System.StringComparison.OrdinalIgnoreCase);
    public bool ShowTitle => string.Equals(ShowTitleRaw, "true", System.StringComparison.OrdinalIgnoreCase);

    public PageDefinition? TargetPage { get; private set; }

    public string DisplayTitle =>
        !string.IsNullOrWhiteSpace(TitleOverride) ? TitleOverride
        : TargetPage?.Title ?? "[未指定画面]";

    /// <summary>嵌入式渲染时填充的子控件元素（位置 + view），由 View 通过 ItemsControl 绑定。</summary>
    public ObservableCollection<PositionedWidget> EmbeddedWidgets { get; } = new();

    /// <summary>是否为占位模式（模态 / 找不到目标页 / 自引用）。</summary>
    public bool IsPlaceholder => IsModal || TargetPage is null;

    public string PlaceholderText =>
        IsModal ? $"[弹窗] {DisplayTitle}"
        : TargetPage is null ? $"[找不到画面] {ReferencedPageRouteKey}"
        : "";

    public Visibility EmbeddedVisibility => IsPlaceholder ? Visibility.Collapsed : Visibility.Visible;
    public Visibility PlaceholderVisibility => IsPlaceholder ? Visibility.Visible : Visibility.Collapsed;
    public Visibility TitleBarVisibility => ShowTitle ? Visibility.Visible : Visibility.Collapsed;

    private void ResolveTargetPage()
    {
        TargetPage = null;
        EmbeddedWidgets.Clear();

        if (string.IsNullOrWhiteSpace(ReferencedPageRouteKey)) return;

        // Shell 可能是 MainViewModel（包含 RuntimeProjectService 实例 / Current ProjectDocument 引用）
        // 或直接拿到 ProjectDocument。通过反射避免硬耦合 ViewModels.MainViewModel。
        ProjectDocument? doc = TryResolveProject(_dataContext.Shell);
        if (doc is null) return;

        var page = doc.Pages?.FirstOrDefault(p =>
            string.Equals(p.RouteKey, ReferencedPageRouteKey, System.StringComparison.OrdinalIgnoreCase));
        if (page is null) return;

        TargetPage = page;

        // 嵌入式：创建子 widgets
        if (!IsModal)
        {
            // 通过 Shell 反射拿到 IWidgetViewFactory（DesignerEditorViewModel.WidgetViewFactory 或注入服务）
            var factory = TryResolveFactory(_dataContext.Shell);
            if (factory is null) return;

            foreach (var w in page.Widgets)
            {
                // 防止自引用 / 无限嵌套：跳过 typeId == screen-window 且引用回当前页或自身
                if (string.Equals(w.TypeId, "screen-window", System.StringComparison.OrdinalIgnoreCase))
                {
                    if (w.Properties.TryGetValue("referencedPageRouteKey", out var nestedKey) &&
                        string.Equals(nestedKey, ReferencedPageRouteKey, System.StringComparison.OrdinalIgnoreCase))
                        continue;
                }
                var view = factory.Create(w, _dataContext);
                EmbeddedWidgets.Add(new PositionedWidget(view, w.X, w.Y));
            }
        }
    }

    private static ProjectDocument? TryResolveProject(object? shell)
    {
        if (shell is null) return null;

        // 1) Shell 自身是 ProjectDocument
        if (shell is ProjectDocument pd) return pd;

        // 2) 尝试 shell.RuntimeProjectService.Current / shell._runtimeProjectService.Current
        var t = shell.GetType();
        foreach (var name in new[] { "RuntimeProjectService", "_runtimeProjectService" })
        {
            var prop = t.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var f    = t.GetField(name,    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var svc  = prop?.GetValue(shell) ?? f?.GetValue(shell);
            if (svc is not null)
            {
                var curProp = svc.GetType().GetProperty("Current");
                if (curProp?.GetValue(svc) is ProjectDocument curDoc) return curDoc;
            }
        }

        // 3) Shell 上直接挂 ProjectDocument 属性
        foreach (var name in new[] { "ProjectDocument", "CurrentProject", "Project" })
        {
            var prop = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (prop?.GetValue(shell) is ProjectDocument doc) return doc;
        }

        return null;
    }

    private static IWidgetViewFactory? TryResolveFactory(object? shell)
    {
        if (shell is null) return null;
        if (shell is IWidgetViewFactory f) return f;

        var t = shell.GetType();
        foreach (var name in new[] { "WidgetViewFactory", "_widgetFactory", "WidgetRegistry" })
        {
            var prop = t.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var field = t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var v = prop?.GetValue(shell) ?? field?.GetValue(shell);
            if (v is IWidgetViewFactory fac) return fac;
        }
        return null;
    }
}
