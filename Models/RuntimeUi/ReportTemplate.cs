#nullable enable
using System.Collections.Generic;

namespace ApexHMI.Models.RuntimeUi;

/// <summary>
/// P9F: 报表模板。挂在 <see cref="ProjectDocument.ReportTemplates"/>，
/// 由 report-view 通过 templateId 引用。第一版极简，留扩展位。
/// </summary>
public class ReportTemplate
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "新报表";
    public string Title { get; set; } = "报表";
    public List<ReportSection> Sections { get; set; } = new();
}

/// <summary>报表节（Text / Table / Chart）。第一版只支持 Text + Table。</summary>
public class ReportSection
{
    /// <summary>节类型：Text / Table / Chart。</summary>
    public string Type { get; set; } = "Text";
    public string Title { get; set; } = "";

    /// <summary>Text 节的正文；可写 {tag:address} 引用。</summary>
    public string Body { get; set; } = "";

    /// <summary>Table 节的 SQL 或数据源描述。第一版只渲染静态行。</summary>
    public string DataSource { get; set; } = "";

    /// <summary>Table 节静态行（CSV-ish）：第一行表头，逗号分隔。</summary>
    public string StaticRowsCsv { get; set; } = "";
}

/// <summary>报表库容器。</summary>
public class ReportLibrary
{
    public List<ReportTemplate> Templates { get; set; } = new();
}
