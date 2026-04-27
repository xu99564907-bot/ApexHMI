using CommunityToolkit.Mvvm.ComponentModel;

namespace ApexHMI.ViewModels.Modules;

public abstract class ModuleViewModelBase : ObservableObject
{
    protected ModuleViewModelBase(MainViewModel shell, string moduleName)
    {
        Shell = shell;
        ModuleName = moduleName;
    }

    protected MainViewModel Shell { get; }

    public MainViewModel ShellViewModel => Shell;

    public string ModuleName { get; }

    public void NavigateTo(string section)
    {
        if (Shell.NavigateCommand.CanExecute(section))
        {
            Shell.NavigateCommand.Execute(section);
        }
    }
}
