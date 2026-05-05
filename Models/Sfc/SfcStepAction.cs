using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ApexHMI.Models;
using ApexHMI.Services;

namespace ApexHMI.Models.Sfc;

public partial class SfcStepAction : ObservableObject
{
    [ObservableProperty] private string deviceType = "Cylinder";
    [ObservableProperty] private int deviceIndex = 1;
    [ObservableProperty] private string deviceName = string.Empty;
    [ObservableProperty] private string actionType = "ToWork";
    [ObservableProperty] private int pointIndex = 1;
    [ObservableProperty] private string customCommand = string.Empty;
    [ObservableProperty] private string customCondition = string.Empty;

    private SfcDeviceOption? _selectedDeviceOption;
    private AxisPointLabel? _selectedAxisPoint;

    /// <summary>选择设备（气缸/轴），自动填充 DeviceIndex 和 DeviceName</summary>
    public SfcDeviceOption? SelectedDeviceOption
    {
        get => _selectedDeviceOption;
        set
        {
            if (_selectedDeviceOption == value) return;
            _selectedDeviceOption = value;
            OnPropertyChanged();
            if (value is not null)
            {
                DeviceIndex = value.Index;
                DeviceName = value.DisplayName;
            }
        }
    }

    /// <summary>选择轴点位，自动填充 PointIndex</summary>
    public AxisPointLabel? SelectedAxisPoint
    {
        get => _selectedAxisPoint;
        set
        {
            if (_selectedAxisPoint == value) return;
            _selectedAxisPoint = value;
            OnPropertyChanged();
            if (value is not null) PointIndex = value.Index;
        }
    }

    /// <summary>由 ViewModel 在创建/DeviceIndex 变化时注入的轴点位列表</summary>
    public ObservableCollection<AxisPointLabel> AxisPointOptions { get; } = new();

    public IReadOnlyList<string> AllDeviceTypes => SfcCodeGeneratorService.DeviceTypes;
    public IReadOnlyList<string> AvailableActionTypes => SfcCodeGeneratorService.GetActionOptions(DeviceType);

    public bool ShowCylinderPicker => DeviceType == "Cylinder";
    public bool ShowAxisPicker => DeviceType == "Axis";
    public bool ShowVacuumPicker => DeviceType == "Vacuum";
    public bool ShowDeviceIndex => DeviceType == "Motor";
    public bool ShowAxisPointPicker => DeviceType == "Axis" && ActionType == "MoveToPoint";
    public bool ShowTimerInput => DeviceType == "Wait" && ActionType == "Timer";
    public bool ShowCustom => DeviceType == "Custom" || (DeviceType == "Wait" && ActionType == "Condition");

    partial void OnDeviceTypeChanged(string v)
    {
        var opts = SfcCodeGeneratorService.GetActionOptions(v);
        if (!opts.Contains(ActionType))
            ActionType = opts.Count > 0 ? opts[0] : string.Empty;
        SelectedDeviceOption = null;
        OnPropertyChanged(nameof(AllDeviceTypes));
        OnPropertyChanged(nameof(AvailableActionTypes));
        OnPropertyChanged(nameof(ShowCylinderPicker));
        OnPropertyChanged(nameof(ShowAxisPicker));
        OnPropertyChanged(nameof(ShowVacuumPicker));
        OnPropertyChanged(nameof(ShowDeviceIndex));
        OnPropertyChanged(nameof(ShowAxisPointPicker));
        OnPropertyChanged(nameof(ShowTimerInput));
        OnPropertyChanged(nameof(ShowCustom));
    }

    partial void OnActionTypeChanged(string v)
    {
        OnPropertyChanged(nameof(ShowAxisPointPicker));
        OnPropertyChanged(nameof(ShowTimerInput));
        OnPropertyChanged(nameof(ShowCustom));
    }
}
