using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ApexHMI.Models;

/// <summary>
/// B3.1: WinCC AlarmControl 6 态生命周期（PDF V18 Alarm class 章节）。
/// <list type="bullet">
///   <item>IN  — Incoming（来 Active，未确认）</item>
///   <item>INA — Incoming Acknowledged（来 Active，已确认）</item>
///   <item>CA  — Came+Acknowledged（已离开 + 已确认）</item>
///   <item>LIA — Left Inactive（已离开但未确认）</item>
///   <item>LCA — Left Confirmed Acknowledged（已离开 + 已确认 + 已清除）</item>
///   <item>CL  — Cleared（操作员手动清除）</item>
/// </list>
/// </summary>
public enum AlarmLifecycleState
{
    IN,
    INA,
    CA,
    LIA,
    LCA,
    CL,
}

public partial class AlarmRecord : ObservableObject
{
    [ObservableProperty]
    private DateTime time = DateTime.Now;

    [ObservableProperty]
    private string level = "Info";

    [ObservableProperty]
    private string source = string.Empty;

    [ObservableProperty]
    private string message = string.Empty;

    [ObservableProperty]
    private bool active = true;

    [ObservableProperty]
    private bool acknowledged;

    [ObservableProperty]
    private DateTime? clearTime;

    [ObservableProperty]
    private string state = "Active";

    [ObservableProperty]
    private int count = 1;

    [ObservableProperty]
    private string acknowledgedBy = string.Empty;

    [ObservableProperty]
    private string handlingSuggestion = string.Empty;

    [ObservableProperty]
    private string causeArchive = string.Empty;

    [ObservableProperty]
    private bool isHighlighted;

    // A7: 操作员处理后写一句备注（保存到 alarm-history.json，持久化）
    [ObservableProperty]
    private string note = string.Empty;

    // A8: 关联流程跳转目标（如 "主线1.STEP040"），运行时由 ResolveRelatedAlarm 填
    [ObservableProperty]
    private string relatedFlowStep = string.Empty;

    // ============ B3.1: WinCC 6 态生命周期（PDF V18 Alarm class）============
    [ObservableProperty]
    private DateTime? ackedAt;

    [ObservableProperty]
    private string ackedBy = string.Empty;

    [ObservableProperty]
    private DateTime? clearedAt;

    [ObservableProperty]
    private bool manuallyCleared;

    /// <summary>B3.1: 6 态自动推断（来源 Active / Acknowledged / ClearedAt / ManuallyCleared）。</summary>
    /// <remarks>
    /// 推断规则（WinCC PDF Alarm class 状态转移）：
    /// <list type="bullet">
    ///   <item>ManuallyCleared = true → CL</item>
    ///   <item>Active=true, !Acked → IN</item>
    ///   <item>Active=true, Acked → INA</item>
    ///   <item>!Active, Acked, ClearedAt有值 → LCA</item>
    ///   <item>!Active, Acked, ClearedAt无值 → CA</item>
    ///   <item>!Active, !Acked → LIA</item>
    /// </list>
    /// </remarks>
    public AlarmLifecycleState LifecycleState
    {
        get
        {
            if (ManuallyCleared) return AlarmLifecycleState.CL;
            if (Active)
                return Acknowledged ? AlarmLifecycleState.INA : AlarmLifecycleState.IN;
            // !Active
            if (Acknowledged)
                return ClearedAt.HasValue ? AlarmLifecycleState.LCA : AlarmLifecycleState.CA;
            return AlarmLifecycleState.LIA;
        }
    }

    /// <summary>B3.1: 未确认（IN / LIA）→ 提示闪烁。</summary>
    public bool RequiresAckBlink =>
        LifecycleState is AlarmLifecycleState.IN or AlarmLifecycleState.LIA;

    // B3.1: 当源属性变化时通知 LifecycleState / RequiresAckBlink 重算
    partial void OnActiveChanged(bool value)        { OnPropertyChanged(nameof(LifecycleState)); OnPropertyChanged(nameof(RequiresAckBlink)); }
    partial void OnAcknowledgedChanged(bool value)  { OnPropertyChanged(nameof(LifecycleState)); OnPropertyChanged(nameof(RequiresAckBlink)); }
    partial void OnClearedAtChanged(DateTime? value) { OnPropertyChanged(nameof(LifecycleState)); OnPropertyChanged(nameof(RequiresAckBlink)); }
    partial void OnManuallyClearedChanged(bool value) { OnPropertyChanged(nameof(LifecycleState)); OnPropertyChanged(nameof(RequiresAckBlink)); }
}
