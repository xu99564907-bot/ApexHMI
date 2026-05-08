using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using ApexHMI.Models;
using ApexHMI.Views.Dialogs;
using Microsoft.Win32;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ApexHMI.ViewModels;

/// <summary>
/// Phase 4 锦上添花 + Phase 3 后续：F5/F6/F7/F8/F9/F10/F11/F12/F14 命令集合。
/// 写在独立 partial 文件，避免污染 Monitor.cs 主文件。
/// </summary>
public partial class MainViewModel
{
    private static readonly JsonSerializerOptions Phase4JsonOptions = new() { WriteIndented = true };

    // ========== F5 M16 OPC UA 节点收藏夹 ==========
    public ObservableCollection<OpcUaFavoriteNode> OpcUaFavorites { get; } = new();

    [RelayCommand]
    private async Task AddOpcUaFavoriteAsync(string? nodeId)
    {
        var id = (nodeId ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(id))
        {
            ShowPopup("收藏失败", "请先选择一个节点", "Warning");
            return;
        }
        if (OpcUaFavorites.Any(f => string.Equals(f.NodeId, id, StringComparison.Ordinal)))
        {
            SystemMessage = $"节点已在收藏夹：{id}";
            return;
        }
        OpcUaFavorites.Add(new OpcUaFavoriteNode
        {
            NodeId = id,
            DisplayName = id,
            AddedAt = DateTime.Now
        });
        await SaveOpcUaFavoritesAsync();
        AddLog("OPC UA", $"已收藏节点：{id}", "Info");
        AddAudit("节点收藏", id, "成功", "加入收藏夹");
    }

    [RelayCommand]
    private async Task RemoveOpcUaFavoriteAsync(OpcUaFavoriteNode? fav)
    {
        if (fav is null) return;
        OpcUaFavorites.Remove(fav);
        await SaveOpcUaFavoritesAsync();
        AddLog("OPC UA", $"已移除收藏：{fav.NodeId}", "Info");
    }

    private async Task SaveOpcUaFavoritesAsync()
    {
        try
        {
            var path = Path.Combine(GetProjectRoot(), "config", "opc-favorites.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var json = JsonSerializer.Serialize(OpcUaFavorites.ToList(), Phase4JsonOptions);
            await Compat.WriteAllTextAsync(path, json);
        }
        catch (Exception ex)
        {
            AddLog("OPC UA", $"保存收藏失败：{ex.Message}", "Warning");
        }
    }

    [RelayCommand]
    private async Task LoadOpcUaFavoritesAsync()
    {
        try
        {
            var path = Path.Combine(GetProjectRoot(), "config", "opc-favorites.json");
            if (!File.Exists(path)) return;
            var json = await Compat.ReadAllTextAsync(path);
            var items = JsonSerializer.Deserialize<List<OpcUaFavoriteNode>>(json, Phase4JsonOptions);
            if (items is null) return;
            OpcUaFavorites.Clear();
            foreach (var i in items) OpcUaFavorites.Add(i);
        }
        catch (Exception ex)
        {
            AddLog("OPC UA", $"加载收藏失败：{ex.Message}", "Warning");
        }
    }

    // ========== F6 M18 OPC UA 节点写入测试 ==========
    [RelayCommand]
    private async Task OpcUaWriteTestAsync(string? nodeId)
    {
        if (CurrentUserRole < UserRole.Engineer)
        {
            ShowPopup("权限不足", "需要工程师及以上角色才能写入 OPC UA 节点", "Warning");
            return;
        }
        if (IsAutoMode)
        {
            ShowPopup("操作禁止", "自动模式下不允许直接写入 OPC UA 节点", "Warning");
            return;
        }
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            ShowPopup("写入测试", "请先选择目标节点", "Warning");
            return;
        }

        var dlg = new OpcUaWriteTestDialog(nodeId)
        {
            Owner = Application.Current?.MainWindow
        };
        if (dlg.ShowDialog() != true) return;

        var beforeValue = GetTagValue(nodeId);
        try
        {
            var tag = FindTagByNameOrNodeId(nodeId);
            if (tag is null)
            {
                ShowPopup("写入失败", $"未找到节点：{nodeId}", "Warning");
                return;
            }
            await _opcUaService.WriteTagAsync(tag, dlg.NewValue);
            await Task.Delay(200);
            var afterValue = GetTagValue(nodeId);
            AddLog("OPC UA", $"写入测试 {nodeId}：{beforeValue} → {dlg.NewValue}（回读 {afterValue}）", "Info");
            AddAudit("OPC UA 写入测试", nodeId, "成功",
                $"输入 {dlg.NewValue}，回读 {afterValue}（写入前 {beforeValue}）");
        }
        catch (Exception ex)
        {
            AddLog("OPC UA", $"写入失败：{ex.Message}", "Error");
            AddAudit("OPC UA 写入测试", nodeId, "失败", ex.Message);
        }
    }

    // ========== F7 M23 异常排行榜跳转 trace 图（HighlightFlow 已存在，加一个 Public） ==========
    [RelayCommand]
    private void JumpToFlowStepCenter(FlowIssueSummary? summary)
    {
        if (summary is null) return;
        var match = summary.Name?.Replace("STEP ", string.Empty);
        if (int.TryParse(match, out var stepNo))
        {
            // 触发 HighlightRequested 让程序监控页 trace 图居中到 stepNo
            HighlightRequested?.Invoke("FlowStep", stepNo.ToString());
        }
        Navigate("程序监控");
    }

    // ========== F8 M24 标记关键步号 ==========
    [RelayCommand]
    private void ToggleCriticalStep(FlowStepRecord? step)
    {
        if (step is null) return;
        step.IsCriticalStep = !step.IsCriticalStep;
        if (!step.IsCriticalStep) step.CriticalNote = string.Empty;
        AddLog("流程", step.IsCriticalStep
            ? $"已标记关键步号 {step.FlowName}/STEP{step.StepNo:000}"
            : $"已取消关键步号 {step.FlowName}/STEP{step.StepNo:000}", "Info");
    }

    // ========== F9 M25 历史回放速率 ==========
    public ObservableCollection<string> ReplayRateOptions { get; } = new() { "1x", "2x", "5x", "10x" };

    [ObservableProperty]
    private string selectedReplayRate = "1x";

    public double ReplayRateMultiplier => SelectedReplayRate switch
    {
        "2x" => 2.0,
        "5x" => 5.0,
        "10x" => 10.0,
        _ => 1.0
    };

    partial void OnSelectedReplayRateChanged(string value) =>
        OnPropertyChanged(nameof(ReplayRateMultiplier));

    // ========== F10 M26 看报警弹层（不离开监控页） ==========
    [RelayCommand]
    private void ShowAlarmPopup(string? alarmKeyword)
    {
        if (string.IsNullOrWhiteSpace(alarmKeyword))
        {
            ShowPopup("报警预览", "未指定报警关键字", "Info");
            return;
        }
        var hits = AlarmHistory
            .Where(a => a.Source.IndexOf(alarmKeyword, StringComparison.OrdinalIgnoreCase) >= 0
                     || a.Message.IndexOf(alarmKeyword, StringComparison.OrdinalIgnoreCase) >= 0)
            .Take(20).ToList();
        var sb = new StringBuilder();
        sb.AppendLine($"关键字：{alarmKeyword}");
        sb.AppendLine($"匹配 {hits.Count} 条最近记录：");
        sb.AppendLine();
        foreach (var a in hits)
        {
            sb.AppendLine($"[{a.Time:HH:mm:ss}] {a.Level} {a.Source}");
            sb.AppendLine($"    {a.Message}");
        }
        MessageBox.Show(sb.ToString(), "报警预览（不离开当前页）",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // ========== F11 M27 流程异常报告 PDF（友好打印 HTML） ==========
    [RelayCommand]
    private async Task ExportFlowIssueReportPdfAsync()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "HTML 报表 (浏览器打印为 PDF)|*.html",
            FileName = $"flow-issue-report-{DateTime.Now:yyyyMMdd-HHmmss}.html"
        };
        if (dialog.ShowDialog() != true) return;

        var html = new StringBuilder();
        html.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'><title>流程异常分析报告</title>");
        html.AppendLine("<style>body{font-family:'Microsoft YaHei',sans-serif;font-size:12px;color:#0F172A;}");
        html.AppendLine("h1{font-size:18px;border-bottom:2px solid #DC2626;padding-bottom:6px;}h2{font-size:14px;margin-top:18px;color:#1D4ED8;}");
        html.AppendLine("table{border-collapse:collapse;width:100%;margin-top:8px;}th,td{border:1px solid #999;padding:4px 8px;text-align:left;}th{background:#FEE2E2;}");
        html.AppendLine(".abnormal{background:#FEF2F2;color:#B91C1C;font-weight:600;}");
        html.AppendLine("@media print{body{margin:1cm;}}</style></head><body>");
        html.AppendLine($"<h1>流程异常分析报告 — {DateTime.Now:yyyy-MM-dd HH:mm:ss}</h1>");
        html.AppendLine($"<p>当前活跃报警 <b style='color:#DC2626'>{ActiveAlarmCount}</b> 条 / 预计停机 {EstimatedDowntimeMinutes:F1} min / 影响产量 {EstimatedProductionLoss}</p>");

        html.AppendLine("<h2>异常排行榜</h2><table><thead><tr><th>类别</th><th>名称</th><th>指标</th><th>结论 / 处理建议</th></tr></thead><tbody>");
        foreach (var s in FlowIssueSummaries)
        {
            html.AppendLine($"<tr><td>{HtmlEsc(s.Category)}</td><td>{HtmlEsc(s.Name)}</td><td>{HtmlEsc(s.Metric)}</td><td>{HtmlEsc(s.Conclusion)}</td></tr>");
        }
        html.AppendLine("</tbody></table>");

        html.AppendLine("<h2>异常流程明细（最近 50 条）</h2><table><thead><tr><th>时间</th><th>流程</th><th>步号</th><th>耗时(s)</th><th>结果</th><th>关联报警</th></tr></thead><tbody>");
        foreach (var step in FlowSteps.Where(x => x.IsAbnormal).Take(50))
        {
            html.AppendLine($"<tr class='abnormal'><td>{step.Time:HH:mm:ss}</td><td>{HtmlEsc(step.FlowName)}</td><td>STEP{step.StepNo:000}</td><td>{step.DurationSeconds:F2}</td><td>{HtmlEsc(step.Result)}</td><td>{HtmlEsc(step.RelatedAlarm)}</td></tr>");
        }
        html.AppendLine("</tbody></table>");

        html.AppendLine("<h2>当前活跃报警</h2><table><thead><tr><th>时间</th><th>级别</th><th>来源</th><th>内容</th></tr></thead><tbody>");
        foreach (var a in CurrentAlarms.Take(20))
        {
            html.AppendLine($"<tr><td>{a.Time:HH:mm:ss}</td><td>{HtmlEsc(a.Level)}</td><td>{HtmlEsc(a.Source)}</td><td>{HtmlEsc(a.Message)}</td></tr>");
        }
        html.AppendLine("</tbody></table>");

        html.AppendLine("<p style='margin-top:18px;color:#64748B;font-size:11px'>提示：浏览器 Ctrl+P → 选择【另存为 PDF】可导出 PDF 文件。</p>");
        html.AppendLine("</body></html>");

        await Compat.WriteAllTextAsync(dialog.FileName, html.ToString(), Encoding.UTF8);
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = dialog.FileName, UseShellExecute = true }); }
        catch { }
        SystemMessage = $"流程异常报告已导出：{dialog.FileName}";
        AddLog("流程", SystemMessage, "Info");
    }

    private static string HtmlEsc(string? s) =>
        string.IsNullOrEmpty(s) ? string.Empty :
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    // ========== F14 H12 首页 KPI 隐藏（个人偏好，持久化到 config/home-prefs.json） ==========
    [ObservableProperty] private bool isKpiHiddenStatus;
    [ObservableProperty] private bool isKpiHiddenProduction;
    [ObservableProperty] private bool isKpiHiddenOee;
    [ObservableProperty] private bool isKpiHiddenAxis;
    [ObservableProperty] private bool isKpiHiddenAlarm;
    [ObservableProperty] private bool isKpiHiddenUnack;

    partial void OnIsKpiHiddenStatusChanged(bool value) => _ = SaveHomePreferencesAsync();
    partial void OnIsKpiHiddenProductionChanged(bool value) => _ = SaveHomePreferencesAsync();
    partial void OnIsKpiHiddenOeeChanged(bool value) => _ = SaveHomePreferencesAsync();
    partial void OnIsKpiHiddenAxisChanged(bool value) => _ = SaveHomePreferencesAsync();
    partial void OnIsKpiHiddenAlarmChanged(bool value) => _ = SaveHomePreferencesAsync();
    partial void OnIsKpiHiddenUnackChanged(bool value) => _ = SaveHomePreferencesAsync();

    private async Task SaveHomePreferencesAsync()
    {
        try
        {
            var path = Path.Combine(GetProjectRoot(), "config", "home-prefs.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var json = JsonSerializer.Serialize(new HomePrefs
            {
                HideStatus = IsKpiHiddenStatus,
                HideProduction = IsKpiHiddenProduction,
                HideOee = IsKpiHiddenOee,
                HideAxis = IsKpiHiddenAxis,
                HideAlarm = IsKpiHiddenAlarm,
                HideUnack = IsKpiHiddenUnack
            }, Phase4JsonOptions);
            await Compat.WriteAllTextAsync(path, json);
        }
        catch { /* ignore */ }
    }

    private sealed class HomePrefs
    {
        public bool HideStatus { get; set; }
        public bool HideProduction { get; set; }
        public bool HideOee { get; set; }
        public bool HideAxis { get; set; }
        public bool HideAlarm { get; set; }
        public bool HideUnack { get; set; }
    }
}
