using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ApexHMI.Models;

public partial class IoMonitorItem : ObservableObject
{
    [ObservableProperty] private int index;                    // 在 Monitor 数组中的索引 [0..15]
    [ObservableProperty] private string address = string.Empty; // 显示的地址文本（如 "DI[0]"）
    [ObservableProperty] private string comment = string.Empty; // IO 注释
    [ObservableProperty] private bool status;                   // 当前 IO 状态
    [ObservableProperty] private string statusTagName = string.Empty;  // OPC UA 变量名 (e.g. "OP80_DI_Mirror.Monitor[0].Status")
    [ObservableProperty] private string commentTagName = string.Empty; // OPC UA 变量名 (e.g. "OP80_DI_Mirror.Monitor[0].Comment")
    [ObservableProperty] private string direction = "DI";      // "DI" 或 "DO"

    // M10 今日翻转计数（识别抖动 / 信号活跃度）
    [ObservableProperty] private int toggleCount;

    // M11 最近一次状态变化时间（用于"近期变化高亮"DataTrigger）
    [ObservableProperty] private DateTime? lastChangeAt;

    /// <summary>1.5 秒内有过状态变化则返回 true。UI 用 DataTrigger 高亮。</summary>
    public bool IsRecentlyChanged =>
        LastChangeAt.HasValue && (DateTime.Now - LastChangeAt.Value).TotalMilliseconds < 1500;

    private bool _ignoreFirstStatusChange = true;

    partial void OnStatusChanged(bool value)
    {
        // 第一次 set 是初始化（_ioMonitorItems 创建时填值），不计入翻转
        if (_ignoreFirstStatusChange) { _ignoreFirstStatusChange = false; return; }
        ToggleCount++;
        LastChangeAt = DateTime.Now;
        OnPropertyChanged(nameof(IsRecentlyChanged));
    }

    /// <summary>由 ViewModel timer 周期调用，让 IsRecentlyChanged 在 1.5s 后自动恢复 false。</summary>
    public void RefreshRecentlyChangedFlag()
    {
        OnPropertyChanged(nameof(IsRecentlyChanged));
    }
}
