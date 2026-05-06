using System.Collections.ObjectModel;

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

    /// <summary>P3.1 模板页 RouteKey：所有页面运行时叠加显示其控件（页眉/页脚/Logo 等）。</summary>
    public string? TemplatePageRouteKey { get; set; }

    /// <summary>
    /// 页面集合（ObservableCollection 让设计器列表/下拉框增删能即时刷新）。
    /// JSON 反序列化兼容 List。
    /// </summary>
    public ObservableCollection<PageDefinition> Pages { get; set; } = new();
}
