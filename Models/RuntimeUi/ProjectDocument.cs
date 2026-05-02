using System.Collections.Generic;

namespace ApexHMI.Models.RuntimeUi;

/// <summary>
/// 开放式 HMI 工程文档（project.json 根对象）。
/// schemaVersion 用于读时迁移：版本不匹配时由 ProjectMigration 执行无损升级。
/// </summary>
public class ProjectDocument
{
    /// <summary>Schema 版本号，当前为 1。升级时由 ProjectMigration 递增。</summary>
    public int SchemaVersion { get; set; } = 1;

    public string ProjectName { get; set; } = "新工程";

    /// <summary>启动时默认显示的页面 RouteKey；null 时显示第一页。</summary>
    public string? DefaultPageRouteKey { get; set; }

    public List<PageDefinition> Pages { get; set; } = new();
}
