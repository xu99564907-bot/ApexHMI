using System.Collections.ObjectModel;
using System.Collections.Generic;
using ApexHMI.Models;

namespace ApexHMI.ViewModels.Modules;

public sealed class ManualViewModel : ModuleViewModelBase
{
    public ManualViewModel(MainViewModel shell)
        : base(shell, "手动操作")
    {
    }

    public string CurrentSubSection => Shell.CurrentManualSubSection;
    public ObservableCollection<ManualCylinderBlockItem> CylinderBlocks => Shell.ManualCylinderBlocks;
    public ObservableCollection<ManualAxisBlockItem> AxisBlocks => Shell.ManualAxisBlocks;
    public IEnumerable<ManualCylinderBlockItem> CylinderCards => Shell.ManualCylinderBlockCards;
    public IEnumerable<ManualAxisBlockItem> AxisCards => Shell.ManualAxisBlockCards;
    public string CylinderStatusText => Shell.CylinderStatusText;
}
