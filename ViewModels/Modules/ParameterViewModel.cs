using System.Collections.ObjectModel;
using System.ComponentModel;
using ApexHMI.Models;

namespace ApexHMI.ViewModels.Modules;

public sealed class ParameterViewModel : ModuleViewModelBase
{
    public ParameterViewModel(MainViewModel shell)
        : base(shell, "参数设定")
    {
    }

    public string CurrentSubSection => Shell.CurrentParameterSubSection;
    public string Title => Shell.CurrentParameterTitle;
    public ObservableCollection<ParameterItem> Parameters => Shell.Parameters;
    public ICollectionView ParametersView => Shell.ParametersView;
    public bool CanEditParameters => Shell.CanEditParameters;
}
