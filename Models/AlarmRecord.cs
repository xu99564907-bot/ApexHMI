using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ApexHMI.Models;

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
}
