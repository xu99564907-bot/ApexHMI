using System.Collections.ObjectModel;
using ApexHMI.Models;

namespace ApexHMI.ViewModels.Modules;

public sealed class AuditViewModel : ModuleViewModelBase
{
    public AuditViewModel(MainViewModel shell)
        : base(shell, "操作审计")
    {
    }

    public ObservableCollection<OperationAuditRecord> OperationAudits => Shell.OperationAudits;
}
