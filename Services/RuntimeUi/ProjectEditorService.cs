using System;
using System.Collections.Generic;
using System.Linq;
using ApexHMI.Models.RuntimeUi;
using Serilog;

namespace ApexHMI.Services.RuntimeUi;

/// <summary>工程编辑器服务实现。</summary>
public sealed class ProjectEditorService : IProjectEditorService
{
    public PageDefinition AddPage(ProjectDocument doc, string title, string? routeKey = null)
    {
        var page = new PageDefinition
        {
            Title = title,
            RouteKey = routeKey ?? SanitizeRouteKey(title),
        };
        doc.Pages.Add(page);
        Log.Information("ProjectEditor: 已添加页面 title={Title} routeKey={RouteKey} id={Id}", title, page.RouteKey, page.Id);
        return page;
    }

    public bool RemovePage(ProjectDocument doc, string pageId, out string? error)
    {
        var page = doc.Pages.FirstOrDefault(p => string.Equals(p.Id, pageId, StringComparison.Ordinal));
        if (page is null)
        {
            error = $"页面不存在：{pageId}";
            return false;
        }

        // B-02 引用检查
        var referencingPages = FindPagesReferencing(doc, pageId);
        if (referencingPages.Count > 0)
        {
            error = $"无法删除页面 \"{page.Title}\"，它被以下页面的跳转动作所引用：{string.Join("、", referencingPages)}";
            Log.Warning("ProjectEditor: 删除被引用页面被阻止 page={Page} referencedBy={Referencers}", page.Title, referencingPages);
            return false;
        }

        doc.Pages.Remove(page);
        Log.Information("ProjectEditor: 已删除页面 title={Title} id={Id}", page.Title, pageId);
        error = null;
        return true;
    }

    public PageDefinition? DuplicatePage(ProjectDocument doc, string pageId)
    {
        var source = doc.Pages.FirstOrDefault(p => string.Equals(p.Id, pageId, StringComparison.Ordinal));
        if (source is null) return null;

        var clone = new PageDefinition
        {
            Title = source.Title + " (副本)",
            RouteKey = source.RouteKey + "-copy",
            RequiredRole = source.RequiredRole,
            CanvasWidth = source.CanvasWidth,
            CanvasHeight = source.CanvasHeight,
            Widgets = source.Widgets.Select(CloneWidget).ToList(),
        };
        doc.Pages.Add(clone);
        Log.Information("ProjectEditor: 已复制页面 from={SourceTitle} to={CloneTitle} id={Id}", source.Title, clone.Title, clone.Id);
        return clone;
    }

    public void ReorderPages(ProjectDocument doc, IReadOnlyList<string> pageIdOrder)
    {
        var lookup = doc.Pages.ToDictionary(p => p.Id, StringComparer.Ordinal);
        var ordered = new List<PageDefinition>(pageIdOrder.Count);

        foreach (var id in pageIdOrder)
        {
            if (lookup.TryGetValue(id, out var page))
            {
                ordered.Add(page);
                lookup.Remove(id);
            }
        }

        // 不在顺序列表中的页追加到末尾
        ordered.AddRange(lookup.Values);
        doc.Pages.Clear();
        doc.Pages.AddRange(ordered);
        Log.Information("ProjectEditor: 页面重排完成 count={Count}", ordered.Count);
    }

    public bool RenamePage(ProjectDocument doc, string pageId, string newTitle, out string? error)
    {
        var page = doc.Pages.FirstOrDefault(p => string.Equals(p.Id, pageId, StringComparison.Ordinal));
        if (page is null)
        {
            error = $"页面不存在：{pageId}";
            return false;
        }

        if (string.IsNullOrWhiteSpace(newTitle))
        {
            error = "页面标题不能为空";
            return false;
        }

        var oldTitle = page.Title;
        page.Title = newTitle;
        Log.Information("ProjectEditor: 页面重命名 from={OldTitle} to={NewTitle}", oldTitle, newTitle);
        error = null;
        return true;
    }

    public IReadOnlyList<string> FindPagesReferencing(ProjectDocument doc, string pageId)
    {
        var targetPage = doc.Pages.FirstOrDefault(p => string.Equals(p.Id, pageId, StringComparison.Ordinal));
        if (targetPage is null) return Array.Empty<string>();

        var targetRouteKey = targetPage.RouteKey;
        var referencePageTitles = new List<string>();

        foreach (var page in doc.Pages)
        {
            foreach (var widget in page.Widgets)
            {
                if (string.Equals(widget.ActionType, "navigate", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(widget.ActionParam, targetRouteKey, StringComparison.OrdinalIgnoreCase))
                {
                    referencePageTitles.Add(page.Title);
                    break;
                }
            }
        }

        return referencePageTitles;
    }

    private static string SanitizeRouteKey(string title)
    {
        return title.ToLowerInvariant()
            .Replace(" ", "-")
            .Replace("（", string.Empty)
            .Replace("）", string.Empty);
    }

    private static WidgetInstance CloneWidget(WidgetInstance source)
    {
        var properties = new Dictionary<string, string>(source.Properties);
        return new WidgetInstance
        {
            TypeId = source.TypeId,
            X = source.X,
            Y = source.Y,
            Width = source.Width,
            Height = source.Height,
            Properties = properties,
            Binding = source.Binding is null ? null : new BindingSpec
            {
                TagId = source.Binding.TagId,
                AccessMode = source.Binding.AccessMode,
                DataType = source.Binding.DataType,
            },
            ActionType = source.ActionType,
            ActionParam = source.ActionParam,
        };
    }
}
