using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using ApexHMI.Models;
using ApexHMI.Views.Dialogs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace ApexHMI.ViewModels.Modules;

public sealed partial class AuditViewModel : ModuleViewModelBase
{
    public AuditViewModel(MainViewModel shell)
        : base(shell, "操作审计")
    {
        OperationAudits.CollectionChanged += (_, _) => RefreshSourceOptions();
        ExportAuditPdfCommand = new AsyncRelayCommand(ExportAuditPdfAsync);
        ArchiveOldAuditsCommand = new RelayCommand(ArchiveOldAudits);
        ShowEventChainCommand = new RelayCommand<OperationAuditRecord?>(ShowEventChain);
        RefreshSourceOptions();
        AuditView.Filter = FilterAudit;
    }

    public ObservableCollection<OperationAuditRecord> OperationAudits => Shell.OperationAudits;
    public ICollectionView AuditView => CollectionViewSource.GetDefaultView(Shell.OperationAudits);

    // AU1 时间段（与报警共用过滤思路）
    public ObservableCollection<string> TimeRangeOptions { get; } =
        new() { "全部", "近1h", "近8h", "今日", "近7天", "近30天" };

    // AU2 / AU6 用户 + 分类下拉，"全部" 由 RefreshSourceOptions 维护
    public ObservableCollection<string> UserOptions { get; } = new() { "全部" };
    public ObservableCollection<string> CategoryOptions { get; } =
        new() { "全部", "登录类", "设备操作", "参数修改", "报警处理", "系统事件", "其他" };

    [ObservableProperty]
    private string selectedTimeRange = "全部";

    [ObservableProperty]
    private string selectedUser = "全部";

    [ObservableProperty]
    private string selectedCategory = "全部";

    [ObservableProperty]
    private string keyword = string.Empty;

    [ObservableProperty]
    private OperationAuditRecord? selectedAudit;

    public IAsyncRelayCommand ExportAuditPdfCommand { get; }
    public IRelayCommand ArchiveOldAuditsCommand { get; }
    public IRelayCommand<OperationAuditRecord?> ShowEventChainCommand { get; }

    partial void OnSelectedTimeRangeChanged(string value) => AuditView.Refresh();
    partial void OnSelectedUserChanged(string value) => AuditView.Refresh();
    partial void OnSelectedCategoryChanged(string value) => AuditView.Refresh();
    partial void OnKeywordChanged(string value) => AuditView.Refresh();

    private void RefreshSourceOptions()
    {
        // AU2: 用户下拉根据现有记录推导
        var users = OperationAudits
            .Select(a => a.User)
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(u => u, StringComparer.Ordinal)
            .ToList();
        var current = SelectedUser;
        UserOptions.Clear();
        UserOptions.Add("全部");
        foreach (var u in users) UserOptions.Add(u);
        if (!UserOptions.Contains(current, StringComparer.Ordinal)) SelectedUser = "全部";
    }

    private bool FilterAudit(object item)
    {
        if (item is not OperationAuditRecord r) return false;
        var now = DateTime.Now;

        // AU1 时间段
        switch (SelectedTimeRange)
        {
            case "近1h": if (r.Time < now.AddHours(-1)) return false; break;
            case "近8h": if (r.Time < now.AddHours(-8)) return false; break;
            case "今日": if (r.Time.Date != now.Date) return false; break;
            case "近7天": if (r.Time < now.AddDays(-7)) return false; break;
            case "近30天": if (r.Time < now.AddDays(-30)) return false; break;
        }

        // AU2 用户
        if (!string.Equals(SelectedUser, "全部", StringComparison.Ordinal)
            && !string.Equals(r.User, SelectedUser, StringComparison.Ordinal)) return false;

        // AU6 分类
        if (!string.Equals(SelectedCategory, "全部", StringComparison.Ordinal)
            && !string.Equals(r.Category, SelectedCategory, StringComparison.Ordinal)) return false;

        // AU3 关键字（target / detail / action）
        var k = (Keyword ?? string.Empty).Trim();
        if (!string.IsNullOrEmpty(k))
        {
            if (r.Action.IndexOf(k, StringComparison.OrdinalIgnoreCase) < 0
                && r.Target.IndexOf(k, StringComparison.OrdinalIgnoreCase) < 0
                && r.Detail.IndexOf(k, StringComparison.OrdinalIgnoreCase) < 0
                && r.User.IndexOf(k, StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// AU4: 导出当前筛选结果为打印友好的 HTML（建议用浏览器打印为 PDF）。
    /// 不引入重量级 PDF 库；HTML + 打印样式即可满足质量审核场景。
    /// </summary>
    private async Task ExportAuditPdfAsync()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "HTML 报表（可打印为 PDF）|*.html|CSV 文件|*.csv",
            FileName = $"audit-report-{DateTime.Now:yyyyMMdd-HHmmss}.html"
        };
        if (dialog.ShowDialog() != true) return;

        var rows = AuditView.Cast<OperationAuditRecord>().ToList();
        var ext = Path.GetExtension(dialog.FileName);
        if (string.Equals(ext, ".csv", StringComparison.OrdinalIgnoreCase))
        {
            var csv = new StringBuilder();
            csv.AppendLine("时间,用户,动作,分类,目标,结果,详情");
            foreach (var r in rows)
            {
                csv.AppendLine(string.Join(",",
                    r.Time.ToString("yyyy-MM-dd HH:mm:ss"),
                    Esc(r.User), Esc(r.Action), Esc(r.Category),
                    Esc(r.Target), Esc(r.Result), Esc(r.Detail)));
            }
            await Compat.WriteAllTextAsync(dialog.FileName, csv.ToString(), Encoding.UTF8);
        }
        else
        {
            var html = new StringBuilder();
            html.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'><title>操作履历审计报表</title>");
            html.AppendLine("<style>body{font-family:'Microsoft YaHei',sans-serif;font-size:12px;color:#0F172A;}");
            html.AppendLine("h1{font-size:18px;border-bottom:2px solid #0F172A;padding-bottom:6px;}");
            html.AppendLine("table{border-collapse:collapse;width:100%;margin-top:10px;}th,td{border:1px solid #999;padding:4px 8px;text-align:left;}th{background:#E2E8F0;}");
            html.AppendLine(".cat{display:inline-block;padding:1px 6px;border-radius:3px;background:#DBEAFE;color:#1D4ED8;font-size:11px;}");
            html.AppendLine("@media print{body{margin:1cm;}}</style></head><body>");
            html.AppendLine($"<h1>操作履历审计报表 — {DateTime.Now:yyyy-MM-dd HH:mm:ss}</h1>");
            html.AppendLine($"<p>筛选条件：时间={SelectedTimeRange} / 用户={SelectedUser} / 分类={SelectedCategory} / 关键字={Esc(Keyword)}</p>");
            html.AppendLine($"<p>共 {rows.Count} 条记录。打印此页（Ctrl+P）→ 选择【另存为 PDF】即可保存为 PDF 报表。</p>");
            html.AppendLine("<table><thead><tr><th>时间</th><th>用户</th><th>分类</th><th>动作</th><th>目标</th><th>结果</th><th>详情</th></tr></thead><tbody>");
            foreach (var r in rows)
            {
                html.AppendLine($"<tr><td>{r.Time:yyyy-MM-dd HH:mm:ss}</td><td>{HtmlEsc(r.User)}</td><td><span class='cat'>{HtmlEsc(r.Category)}</span></td><td>{HtmlEsc(r.Action)}</td><td>{HtmlEsc(r.Target)}</td><td>{HtmlEsc(r.Result)}</td><td>{HtmlEsc(r.Detail)}</td></tr>");
            }
            html.AppendLine("</tbody></table></body></html>");
            await Compat.WriteAllTextAsync(dialog.FileName, html.ToString(), Encoding.UTF8);

            // 自动打开浏览器，方便用户立刻打印
            try { Process.Start(new ProcessStartInfo { FileName = dialog.FileName, UseShellExecute = true }); }
            catch { /* ignore */ }
        }

        Shell.SystemMessage = $"已导出 {rows.Count} 条履历到：{dialog.FileName}";
        Shell.AddLog("审计", Shell.SystemMessage, "Info");
    }

    /// <summary>
    /// AU8: 把 6 个月前的审计记录写入 audit-archive/audit-{yyyyMMdd}.zip，
    /// 内存中只保留最近 6 个月。
    /// </summary>
    private void ArchiveOldAudits()
    {
        var threshold = DateTime.Now.AddMonths(-6);
        var oldOnes = OperationAudits.Where(a => a.Time < threshold).ToList();
        if (oldOnes.Count == 0)
        {
            Shell.SystemMessage = "暂无超过 6 个月的履历，无需归档";
            Shell.AddLog("审计", Shell.SystemMessage, "Info");
            return;
        }

        if (!Shell.RequestConfirmation("履历归档", $"将把 {oldOnes.Count} 条 6 个月前的审计记录写入 zip 并从内存中移除，确认？"))
        {
            return;
        }

        try
        {
            var dir = Path.Combine(Shell.GetProjectRoot(), "config", "audit-archive");
            Directory.CreateDirectory(dir);

            var csv = new StringBuilder();
            csv.AppendLine("时间,用户,动作,分类,目标,结果,详情");
            foreach (var r in oldOnes)
            {
                csv.AppendLine(string.Join(",",
                    r.Time.ToString("yyyy-MM-dd HH:mm:ss"),
                    Esc(r.User), Esc(r.Action), Esc(r.Category),
                    Esc(r.Target), Esc(r.Result), Esc(r.Detail)));
            }

            var zipPath = Path.Combine(dir, $"audit-{DateTime.Now:yyyyMMdd-HHmmss}.zip");
            using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                var entry = zip.CreateEntry("audit.csv", CompressionLevel.Optimal);
                using var es = entry.Open();
                var bytes = Encoding.UTF8.GetBytes(csv.ToString());
                es.Write(bytes, 0, bytes.Length);
            }

            foreach (var r in oldOnes) OperationAudits.Remove(r);

            Shell.SystemMessage = $"已归档 {oldOnes.Count} 条到：{zipPath}";
            Shell.AddLog("审计", Shell.SystemMessage, "Info");
            Shell.AddAudit("审计归档", Path.GetFileName(zipPath), "成功", $"归档 {oldOnes.Count} 条 6 个月前记录");
        }
        catch (Exception ex)
        {
            Shell.ShowPopup("归档失败", ex.Message, "Error");
        }
    }

    /// <summary>
    /// AU5: 给定一条审计记录，按 Target 关键字找出关联的审计 + 报警历史，按时间排序展示。
    /// 用于追溯一个事件链：报警触发 → 操作员确认 → 工程师复位 → 系统恢复。
    /// </summary>
    private void ShowEventChain(OperationAuditRecord? anchor)
    {
        anchor ??= SelectedAudit;
        if (anchor is null)
        {
            Shell.ShowPopup("事件链", "请先在表中选中一条审计记录", "Warning");
            return;
        }

        var keyword = anchor.Target;
        var chain = new System.Collections.Generic.List<EventChainEntry>();
        foreach (var a in OperationAudits.Where(a => a.Target.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0))
        {
            chain.Add(new EventChainEntry { Time = a.Time, Source = "审计", Description = $"{a.Action} / {a.Result}", Detail = a.Detail });
        }
        foreach (var a in Shell.AlarmHistory.Where(a => a.Source.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0
                                                    || a.Message.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0))
        {
            chain.Add(new EventChainEntry { Time = a.Time, Source = "报警", Description = $"{a.Level} / {a.State}", Detail = a.Message });
        }
        var sorted = chain.OrderBy(c => c.Time).ToList();

        var dialog = new EventChainDialog
        {
            Owner = Application.Current?.MainWindow,
            DataContext = new EventChainDialog.EventChainViewModel { Keyword = keyword, Entries = sorted }
        };
        dialog.ShowDialog();
    }

    private static string Esc(string? s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        if (s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0) return s;
        return "\"" + s.Replace("\"", "\"\"") + "\"";
    }

    private static string HtmlEsc(string? s) =>
        string.IsNullOrEmpty(s) ? string.Empty :
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}

public sealed class EventChainEntry
{
    public DateTime Time { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
}
