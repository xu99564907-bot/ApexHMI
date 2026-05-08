using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ApexHMI.Models;

public partial class FlowStepRecord : ObservableObject
{
    [ObservableProperty]
    private string flowId = string.Empty;

    [ObservableProperty]
    private string flowName = string.Empty;

    [ObservableProperty]
    private DateTime time = DateTime.Now;

    [ObservableProperty]
    private DateTime startTime = DateTime.Now;

    [ObservableProperty]
    private DateTime endTime = DateTime.Now;

    [ObservableProperty]
    private double durationSeconds;

    [ObservableProperty]
    private int stepNo;

    [ObservableProperty]
    private string icon = "●";

    [ObservableProperty]
    private string title = string.Empty;

    [ObservableProperty]
    private string comment = string.Empty;

    [ObservableProperty]
    private string result = "运行中";

    [ObservableProperty]
    private string relatedAlarm = string.Empty;

    [ObservableProperty]
    private bool isAbnormal;

    [ObservableProperty]
    private string shiftKey = string.Empty;

    [ObservableProperty]
    private string archiveDate = string.Empty;

    [ObservableProperty]
    private bool isHighlighted;

    // M24 标记关键步号：true 时表 + 图都高亮，便于交班讲解
    [ObservableProperty]
    private bool isCriticalStep;

    // M24 标记备注（一句话说明这步为啥关键）
    [ObservableProperty]
    private string criticalNote = string.Empty;
}
