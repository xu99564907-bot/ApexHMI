using CommunityToolkit.Mvvm.ComponentModel;

namespace ApexHMI.Models;

/// <summary>A5: 报警发生频率直方图的一根 bar（24h-bin 或 7d-bin）。</summary>
public partial class AlarmHistogramBar : ObservableObject
{
    [ObservableProperty]
    private string label = string.Empty;

    [ObservableProperty]
    private int alarmCount;

    [ObservableProperty]
    private int errorCount;

    [ObservableProperty]
    private int warningCount;

    public int Total => AlarmCount + ErrorCount + WarningCount;

    /// <summary>用于 XAML 绘制时换算高度（按最高 bar 归一化，UI 端会乘以 maxHeight）。</summary>
    [ObservableProperty]
    private double normalizedHeight;
}
