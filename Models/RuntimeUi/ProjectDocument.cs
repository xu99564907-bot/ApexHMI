#nullable enable
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

    /// <summary>P6A: 工程级全局样式（色板 + 字体预设）。控件属性值可写
    /// <c>{style:colors/primary}</c> 引用，由 StyleResolver 解析。
    /// 旧工程加载时为空，由 ProjectMigration 注入默认值，保证向后兼容。</summary>
    public StyleDefinitions? Styles { get; set; } = new();

    /// <summary>P6B: 工程级多语言文本资源。控件文本字段可写 <c>{text:welcome}</c> 引用。</summary>
    public TextResources? Texts { get; set; } = new();

    /// <summary>P6C: 工程级控件库（可被拖入画布或从画布右键存入）。</summary>
    public ProjectLibrary? Library { get; set; } = new();

    /// <summary>P6E: 工程级文本/图形列表资源（INT → 文字 / INT → 图片）。
    /// io-symbolic 可写 <c>{textList:status}</c>、io-graphic 可写 <c>{graphicList:state}</c> 引用。</summary>
    public ListResources? Lists { get; set; } = new();

    /// <summary>P7: 工程级 Faceplate 库（可复用 / 有接口属性 / 有版本号的复合控件模板）。</summary>
    public FaceplateLibrary? Faceplates { get; set; } = new();
}
