#nullable enable
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using ApexHMI.Models.RuntimeUi;
using ApexHMI.Services.RuntimeUi;

namespace ApexHMI.ViewModels.Runtime;

/// <summary>
/// P9F: 报表视图。按 ProjectDocument.Reports 中的 ReportTemplate 生成 FlowDocument 预览，
/// 支持打印 / 导出 PDF（通过 XpsDocumentWriter + 系统 PDF 打印机）/ 导出 CSV。
/// <para>导出 PDF 实现：调用 PrintDialog 让用户选 "Microsoft Print to PDF" 打印机，
/// 系统会弹出文件对话框；这是 net48 内置最稳的 PDF 导出方案，零外部依赖。</para>
/// </summary>
public partial class ReportViewWidgetViewModel : WidgetViewModelBase
{
    private DispatcherTimer? _timer;

    public ReportViewWidgetViewModel(WidgetInstance model, IWidgetDataContext dataContext)
        : base(model, dataContext)
    {
        RebuildDocument();

        if (AutoRefresh && RefreshInterval > 0)
        {
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(RefreshInterval) };
            _timer.Tick += (_, __) => RebuildDocument();
            _timer.Start();
        }
    }

    public string TemplateId      => Prop("templateId", "");
    public bool   AutoRefresh     => string.Equals(Prop("autoRefresh", "false"), "true", StringComparison.OrdinalIgnoreCase);
    public double RefreshInterval
    {
        get
        {
            if (double.TryParse(Prop("refreshInterval", "10"), NumberStyles.Any, CultureInfo.InvariantCulture, out var v) && v > 0)
                return v;
            return 10;
        }
    }

    [ObservableProperty] private FlowDocument? _document;
    [ObservableProperty] private string _templateName = "";

    private ReportTemplate? ResolveTemplate()
    {
        var lib = DesignerContext.Document?.Reports;
        if (lib is null) return null;
        if (string.IsNullOrEmpty(TemplateId)) return lib.Templates.FirstOrDefault();
        return lib.Templates.FirstOrDefault(t => string.Equals(t.Id, TemplateId, StringComparison.Ordinal));
    }

    [RelayCommand]
    private void Refresh() => RebuildDocument();

    private void RebuildDocument()
    {
        var tpl = ResolveTemplate();
        var doc = new FlowDocument
        {
            FontFamily = new FontFamily("Segoe UI, Microsoft YaHei"),
            FontSize = 12,
            PagePadding = new Thickness(40),
            ColumnWidth = double.PositiveInfinity,
        };

        if (tpl is null)
        {
            doc.Blocks.Add(new Paragraph(new Run("[未选择报表模板或模板不存在]")) { Foreground = Brushes.Gray });
            Document = doc;
            TemplateName = "(无)";
            return;
        }

        TemplateName = tpl.Name;
        doc.Blocks.Add(new Paragraph(new Run(tpl.Title)) { FontSize = 20, FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Center });
        doc.Blocks.Add(new Paragraph(new Run($"生成时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}")) { Foreground = Brushes.Gray, FontSize = 10, TextAlignment = TextAlignment.Center });

        foreach (var sec in tpl.Sections)
        {
            if (!string.IsNullOrEmpty(sec.Title))
            {
                doc.Blocks.Add(new Paragraph(new Run(sec.Title)) { FontSize = 14, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 10, 0, 4) });
            }
            switch ((sec.Type ?? "Text").ToLowerInvariant())
            {
                case "table":
                    doc.Blocks.Add(BuildTable(sec));
                    break;
                case "chart":
                    doc.Blocks.Add(new Paragraph(new Run("[图表节 — TODO P10 渲染]")) { Foreground = Brushes.DarkGray });
                    break;
                default:
                    doc.Blocks.Add(new Paragraph(new Run(sec.Body ?? "")));
                    break;
            }
        }

        Document = doc;
    }

    private static Block BuildTable(ReportSection sec)
    {
        var lines = (sec.StaticRowsCsv ?? "").Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0)
            return new Paragraph(new Run("[空数据]")) { Foreground = Brushes.Gray };

        var rows = lines.Select(l => l.Split(',')).ToList();
        int cols = rows.Max(r => r.Length);
        var t = new System.Windows.Documents.Table { CellSpacing = 0, BorderBrush = Brushes.Black, BorderThickness = new Thickness(0.5) };
        for (int i = 0; i < cols; i++) t.Columns.Add(new TableColumn());

        var headerGroup = new TableRowGroup();
        var hdrRow = new System.Windows.Documents.TableRow { Background = new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9)) };
        for (int i = 0; i < cols; i++)
        {
            var text = i < rows[0].Length ? rows[0][i] : "";
            hdrRow.Cells.Add(MakeCell(text, true));
        }
        headerGroup.Rows.Add(hdrRow);
        t.RowGroups.Add(headerGroup);

        var bodyGroup = new TableRowGroup();
        for (int r = 1; r < rows.Count; r++)
        {
            var tr = new System.Windows.Documents.TableRow();
            for (int i = 0; i < cols; i++)
            {
                var text = i < rows[r].Length ? rows[r][i] : "";
                tr.Cells.Add(MakeCell(text, false));
            }
            bodyGroup.Rows.Add(tr);
        }
        t.RowGroups.Add(bodyGroup);
        return t;
    }

    private static System.Windows.Documents.TableCell MakeCell(string text, bool header)
    {
        return new System.Windows.Documents.TableCell(new Paragraph(new Run(text ?? ""))
        {
            FontWeight = header ? FontWeights.Bold : FontWeights.Normal,
        })
        {
            BorderBrush = Brushes.LightGray,
            BorderThickness = new Thickness(0.5),
            Padding = new Thickness(4, 2, 4, 2),
        };
    }

    [RelayCommand]
    private void Print()
    {
        if (Document is null) return;
        try
        {
            var dlg = new PrintDialog();
            if (dlg.ShowDialog() != true) return;
            IDocumentPaginatorSource src = Document;
            dlg.PrintDocument(src.DocumentPaginator, TemplateName);
        }
        catch { /* ignore */ }
    }

    /// <summary>
    /// 导出 PDF：用 PrintDialog 让用户选 "Microsoft Print to PDF" 打印机；这是 net48 内置零依赖方案。
    /// TODO P10: 改为直接 XpsDocumentWriter → PDF 转换（需引入第三方库，如 PdfSharp）。
    /// </summary>
    [RelayCommand]
    private void ExportPdf() => Print();

    [RelayCommand]
    private void ExportCsv()
    {
        var tpl = ResolveTemplate();
        if (tpl is null) return;
        var dlg = new SaveFileDialog { Filter = "CSV 文件|*.csv", FileName = $"{tpl.Name}-{DateTime.Now:yyyyMMdd-HHmmss}.csv" };
        if (dlg.ShowDialog() != true) return;

        var sb = new StringBuilder();
        sb.AppendLine($"# {tpl.Title}");
        sb.AppendLine($"# 生成时间,{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        foreach (var sec in tpl.Sections)
        {
            sb.AppendLine();
            sb.AppendLine($"## {sec.Title}");
            if (string.Equals(sec.Type, "Table", StringComparison.OrdinalIgnoreCase))
            {
                sb.AppendLine(sec.StaticRowsCsv ?? "");
            }
            else
            {
                sb.AppendLine(sec.Body ?? "");
            }
        }
        try { File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(true)); } catch { /* ignore */ }
    }
}
