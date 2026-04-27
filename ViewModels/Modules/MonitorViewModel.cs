using System.Collections.ObjectModel;
using ApexHMI.Models;

namespace ApexHMI.ViewModels.Modules;

public sealed class MonitorViewModel : ModuleViewModelBase
{
    public MonitorViewModel(MainViewModel shell)
        : base(shell, "监控")
    {
    }

    public string CurrentSubSection => Shell.CurrentMonitorSubSection;
    public ObservableCollection<TagItem> Tags => Shell.Tags;
    public ObservableCollection<OpcUaBrowseNode> OpcUaBrowserNodes => Shell.OpcUaBrowserNodes;
    public ObservableCollection<FlowStepRecord> FlowSteps => Shell.FlowSteps;
    public ObservableCollection<TrendSample> TrendSamples => Shell.TrendSamples;
    public string CommunicationStatus => Shell.CommunicationStatus;
}
