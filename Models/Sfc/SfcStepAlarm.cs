using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ApexHMI.Models.Sfc;

public partial class SfcStepAlarm : ObservableObject
{
    [ObservableProperty] private string alarmMessage = string.Empty;
    [ObservableProperty] private string alarmCondition = string.Empty;
    [ObservableProperty] private string alarmType = "Stop";

    public static IReadOnlyList<string> AlarmTypes { get; } = new[] { "Estop", "Stop", "Run" };
}
