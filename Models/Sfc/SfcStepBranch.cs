using CommunityToolkit.Mvvm.ComponentModel;

namespace ApexHMI.Models.Sfc;

public partial class SfcStepBranch : ObservableObject
{
    [ObservableProperty] private string condition = string.Empty;
    [ObservableProperty] private string targetStep = "END";
}
