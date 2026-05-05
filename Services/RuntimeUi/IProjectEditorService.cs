using System.Collections.Generic;
using ApexHMI.Models.RuntimeUi;

namespace ApexHMI.Services.RuntimeUi;

/// <summary>工程编辑器服务：增删改页面、重排、引用校验。</summary>
public interface IProjectEditorService
{
    /// <summary>添加新页面，自动生成 Id 和 RouteKey（若 routeKey 为空则用 title 小写化）。</summary>
    PageDefinition AddPage(ProjectDocument doc, string title, string? routeKey = null);

    /// <summary>删除指定 Id 的页面。若被其他页面的 navigate 动作引用则返回 false + error。</summary>
    bool RemovePage(ProjectDocument doc, string pageId, out string? error);

    /// <summary>复制指定页面并追加到工程末尾，返回副件。</summary>
    PageDefinition? DuplicatePage(ProjectDocument doc, string pageId);

    /// <summary>按给定 ID 顺序重排页面列表。不在列表中的页会被追加到末尾。</summary>
    void ReorderPages(ProjectDocument doc, IReadOnlyList<string> pageIdOrder);

    /// <summary>重命名页面标题。</summary>
    bool RenamePage(ProjectDocument doc, string pageId, string newTitle, out string? error);

    /// <summary>检查指定页是否被其他页面的 navigate 动作引用。</summary>
    IReadOnlyList<string> FindPagesReferencing(ProjectDocument doc, string pageId);
}
