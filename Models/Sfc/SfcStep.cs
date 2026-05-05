using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using ApexHMI.Services;

namespace ApexHMI.Models.Sfc;

public partial class SfcStep : ObservableObject
{
    [ObservableProperty] private int stepNo;
    [ObservableProperty] private string completionCondition = string.Empty;
    [ObservableProperty] private string nextStep = "END";

    public ObservableCollection<SfcStepAction> Actions { get; } = new();
    public ObservableCollection<SfcStepBranch> Branches { get; } = new();
    public ObservableCollection<SfcStepAlarm> AlarmEntries { get; } = new();

    public string StepLabel => $"STEP {StepNo:000}";

    public string ActionSummary => Actions.Count == 0
        ? "(空)"
        : string.Join("+", Actions.Select(a =>
            $"{SfcCodeGeneratorService.GetDeviceTypeLabel(a.DeviceType)}{a.DeviceIndex}"));

    public string NextSummary => Branches.Count > 0
        ? $"{Branches.Count}分支"
        : (string.IsNullOrEmpty(NextStep) ? "END" : NextStep);

    public SfcStep()
    {
        Actions.CollectionChanged += (_, _) => OnPropertyChanged(nameof(ActionSummary));
        Branches.CollectionChanged += (_, _) => OnPropertyChanged(nameof(NextSummary));
    }

    partial void OnNextStepChanged(string v) => OnPropertyChanged(nameof(NextSummary));
}
