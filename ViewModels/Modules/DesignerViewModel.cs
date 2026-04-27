using System.Collections.ObjectModel;
using ApexHMI.Models;

namespace ApexHMI.ViewModels.Modules;

public sealed class DesignerViewModel : ModuleViewModelBase
{
    public DesignerViewModel(MainViewModel shell)
        : base(shell, "设计器")
    {
    }

    public string CurrentSubSection => Shell.CurrentDesignerSubSection;
    public ObservableCollection<DesignerPage> Pages => Shell.DesignerPages;
    public ObservableCollection<DesignerElement> Elements => Shell.DesignerElements;
    public ObservableCollection<IoTableRow> IoTableRows => Shell.IoTableRows;
    public ObservableCollection<GeneratedProgramArtifact> GeneratedIoPrograms => Shell.GeneratedIoPrograms;
    public ObservableCollection<GeneratedProgramArtifact> GeneratedAutoPrograms => Shell.GeneratedAutoPrograms;
    public bool IsRuntimeMode => Shell.IsRuntimeMode;
}
