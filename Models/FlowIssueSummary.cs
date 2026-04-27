using CommunityToolkit.Mvvm.ComponentModel;

namespace ApexHMI.Models;

public partial class FlowIssueSummary : ObservableObject
{
    [ObservableProperty]
    private string category = string.Empty;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string metric = string.Empty;

    [ObservableProperty]
    private string conclusion = string.Empty;
}
