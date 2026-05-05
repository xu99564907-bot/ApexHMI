using System.Collections.ObjectModel;
using System.Linq;
using ApexHMI.Models;
using ApexHMI.Models.RuntimeUi;
using ApexHMI.Services.RuntimeUi;
using ApexHMI.ViewModels;

namespace ApexHMI.ViewModels.Runtime;

/// <summary>
/// 报警列表 widget：复用 Shell.CurrentAlarms 集合。
/// Properties:
///   "filterLevel"   过滤等级（Info/Warning/Error/Critical）；空=全部
///   "filterSource"  过滤来源（部分匹配）；空=全部
///   "maxRows"       最多显示行数；默认 20
///   "onlyActive"    只显示 Active=True 的；默认 true
/// </summary>
public partial class AlarmListWidgetViewModel : WidgetViewModelBase
{
    private readonly MainViewModel? _shell;

    public AlarmListWidgetViewModel(WidgetInstance model, IWidgetDataContext dataContext)
        : base(model, dataContext)
    {
        _shell = dataContext.Shell as MainViewModel;
        Refresh();
        if (_shell is not null)
            _shell.CurrentAlarms.CollectionChanged += (_, __) => Refresh();
    }

    public ObservableCollection<AlarmRecord> Alarms { get; } = new();

    private void Refresh()
    {
        Alarms.Clear();
        if (_shell is null) return;

        var level = Prop("filterLevel", string.Empty);
        var source = Prop("filterSource", string.Empty);
        var maxRowsStr = Prop("maxRows", "20");
        if (!int.TryParse(maxRowsStr, out var maxRows) || maxRows <= 0) maxRows = 20;
        var onlyActive = !string.Equals(Prop("onlyActive", "true"), "false", System.StringComparison.OrdinalIgnoreCase);

        var query = _shell.CurrentAlarms.AsEnumerable();
        if (onlyActive) query = query.Where(a => a.Active);
        if (!string.IsNullOrWhiteSpace(level))
            query = query.Where(a => string.Equals(a.Level, level, System.StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(source))
            query = query.Where(a => a.Source?.IndexOf(source, System.StringComparison.OrdinalIgnoreCase) >= 0);

        foreach (var rec in query.OrderByDescending(a => a.Time).Take(maxRows))
            Alarms.Add(rec);
    }
}
