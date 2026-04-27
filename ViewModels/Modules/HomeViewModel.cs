namespace ApexHMI.ViewModels.Modules;

public sealed class HomeViewModel : ModuleViewModelBase
{
    public HomeViewModel(MainViewModel shell)
        : base(shell, "主界面")
    {
    }

    public string CurrentSection => Shell.CurrentSection;
    public string SystemMessage => Shell.SystemMessage;
    public string DeviceStatusText => Shell.DeviceStatusText;
    public string ShiftStatusText => Shell.ShiftStatusText;
}
