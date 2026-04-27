using System.Collections.ObjectModel;
using System.ComponentModel;
using ApexHMI.Models;

namespace ApexHMI.ViewModels.Modules;

public sealed class AlarmViewModel : ModuleViewModelBase
{
    public AlarmViewModel(MainViewModel shell)
        : base(shell, "报警画面")
    {
    }

    public string CurrentSubSection => Shell.CurrentAlarmSubSection;
    public ObservableCollection<AlarmRecord> CurrentAlarms => Shell.CurrentAlarms;
    public ObservableCollection<AlarmRecord> AlarmHistory => Shell.AlarmHistory;
    public ObservableCollection<AlarmRecord> AlarmStatistics => Shell.AlarmStatistics;
    public ICollectionView AlarmStatisticsView => Shell.AlarmStatisticsView;
    public int ActiveAlarmCount => Shell.ActiveAlarmCount;
    public int UnacknowledgedAlarmCount => Shell.UnacknowledgedAlarmCount;
}
