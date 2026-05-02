using System.Collections.Generic;
using System.Collections.ObjectModel;
using ApexHMI.Models;
using CommunityToolkit.Mvvm.Input;

namespace ApexHMI.ViewModels.Modules;

public sealed class ManualViewModel : ModuleViewModelBase
{
    public ManualViewModel(MainViewModel shell)
        : base(shell, "手动操作")
    {
        ToggleDeviceCommand = new AsyncRelayCommand<string?>(tagName => Shell.ToggleDeviceCommand.ExecuteAsync(tagName));
        CylinderMoveToHomeCommand = new AsyncRelayCommand<ManualCylinderBlockItem?>(block => Shell.CylinderMoveToHomeCommand.ExecuteAsync(block));
        CylinderMoveToWorkCommand = new AsyncRelayCommand<ManualCylinderBlockItem?>(block => Shell.CylinderMoveToWorkCommand.ExecuteAsync(block));
        ToggleCylinderHomeMaskCommand = new RelayCommand(() => Shell.ToggleCylinderHomeMaskCommand.Execute(null));
        ToggleCylinderWorkMaskCommand = new RelayCommand(() => Shell.ToggleCylinderWorkMaskCommand.Execute(null));
        SetDebugModeCommand = new AsyncRelayCommand(() => Shell.SetDebugModeCommand.ExecuteAsync(null));
        SetDryRunModeCommand = new AsyncRelayCommand(() => Shell.SetDryRunModeCommand.ExecuteAsync(null));
        SetBypassStationModeCommand = new AsyncRelayCommand(() => Shell.SetBypassStationModeCommand.ExecuteAsync(null));
        SetManualModeCommand = new AsyncRelayCommand(() => Shell.SetManualModeCommand.ExecuteAsync(null));
        SetAutoModeCommand = new AsyncRelayCommand(() => Shell.SetAutoModeCommand.ExecuteAsync(null));
        StartDeviceCommand = new AsyncRelayCommand(() => Shell.StartDeviceCommand.ExecuteAsync(null));
        StopDeviceCommand = new AsyncRelayCommand(() => Shell.StopDeviceCommand.ExecuteAsync(null));
        ResetAlarmFromHomeCommand = new AsyncRelayCommand(() => Shell.ResetAlarmFromHomeCommand.ExecuteAsync(null));
        ResetMotorFaultCommand = new AsyncRelayCommand(() => Shell.ResetMotorFaultCommand.ExecuteAsync(null));
        PauseRobotCommand = new AsyncRelayCommand(() => Shell.PauseRobotCommand.ExecuteAsync(null));
        ResetRobotCommand = new AsyncRelayCommand(() => Shell.ResetRobotCommand.ExecuteAsync(null));
        ToggleAxisEnableCommand = new AsyncRelayCommand(() => Shell.ToggleAxisEnableCommand.ExecuteAsync(null));
        AxisAlarmResetCommand = new AsyncRelayCommand(() => Shell.AxisAlarmResetCommand.ExecuteAsync(null));
    }

    // -- Commands --
    public IAsyncRelayCommand<string?> ToggleDeviceCommand { get; }
    public IAsyncRelayCommand<ManualCylinderBlockItem?> CylinderMoveToHomeCommand { get; }
    public IAsyncRelayCommand<ManualCylinderBlockItem?> CylinderMoveToWorkCommand { get; }
    public IRelayCommand ToggleCylinderHomeMaskCommand { get; }
    public IRelayCommand ToggleCylinderWorkMaskCommand { get; }
    public IAsyncRelayCommand SetDebugModeCommand { get; }
    public IAsyncRelayCommand SetDryRunModeCommand { get; }
    public IAsyncRelayCommand SetBypassStationModeCommand { get; }
    public IAsyncRelayCommand SetManualModeCommand { get; }
    public IAsyncRelayCommand SetAutoModeCommand { get; }
    public IAsyncRelayCommand StartDeviceCommand { get; }
    public IAsyncRelayCommand StopDeviceCommand { get; }
    public IAsyncRelayCommand ResetAlarmFromHomeCommand { get; }
    public IAsyncRelayCommand ResetMotorFaultCommand { get; }
    public IAsyncRelayCommand PauseRobotCommand { get; }
    public IAsyncRelayCommand ResetRobotCommand { get; }
    public IAsyncRelayCommand ToggleAxisEnableCommand { get; }
    public IAsyncRelayCommand AxisAlarmResetCommand { get; }

    // -- Navigation/Sub-section --
    public string CurrentSubSection => Shell.CurrentManualSubSection;
    public string CurrentManualTitle => Shell.CurrentManualTitle;
    public bool IsManualCylinderPageVisible => Shell.IsManualCylinderPageVisible;
    public bool IsManualAxisPageVisible => Shell.IsManualAxisPageVisible;
    public bool IsManualRobotPageVisible => Shell.IsManualRobotPageVisible;

    // -- Cylinder collections --
    public ObservableCollection<ManualCylinderBlockItem> CylinderBlocks => Shell.ManualCylinderBlocks;
    public IEnumerable<ManualCylinderBlockItem> CylinderCards => Shell.ManualCylinderBlockCards;
    public string CylinderStatusText => Shell.CylinderStatusText;
    public bool CylinderHomeMaskEnabled
    {
        get => Shell.CylinderHomeMaskEnabled;
        set => Shell.CylinderHomeMaskEnabled = value;
    }
    public bool CylinderWorkMaskEnabled
    {
        get => Shell.CylinderWorkMaskEnabled;
        set => Shell.CylinderWorkMaskEnabled = value;
    }
    public ManualCylinderBlockItem? SelectedCylinderSettingsBlock
    {
        get => Shell.SelectedCylinderSettingsBlock;
        set => Shell.SelectedCylinderSettingsBlock = value;
    }

    // -- Axis collections --
    public ObservableCollection<ManualAxisBlockItem> AxisBlocks => Shell.ManualAxisBlocks;
    public IEnumerable<ManualAxisBlockItem> AxisCards => Shell.ManualAxisBlockCards;
    public ManualAxisBlockItem? SelectedAxisSettingsBlock
    {
        get => Shell.SelectedAxisSettingsBlock;
        set => Shell.SelectedAxisSettingsBlock = value;
    }

    // -- Robot --
    public RobotControlViewModel? RobotControlViewModel => Shell.RobotControlViewModel;

    // -- Manual write --
    public string ManualWriteTagName
    {
        get => Shell.ManualWriteTagName;
        set => Shell.ManualWriteTagName = value;
    }
    public string ManualWriteValue
    {
        get => Shell.ManualWriteValue;
        set => Shell.ManualWriteValue = value;
    }

    // -- Cylinder settings --
    public string CylinderConfiguredName => Shell.CylinderConfiguredName;
    public string CylinderHomeCommandTagName => Shell.CylinderHomeCommandTagName;
    public string CylinderWorkCommandTagName => Shell.CylinderWorkCommandTagName;
    public string CylinderHomeSensorTagName => Shell.CylinderHomeSensorTagName;
    public string CylinderWorkSensorTagName => Shell.CylinderWorkSensorTagName;
    public string CylinderHomeInterlockTagName => Shell.CylinderHomeInterlockTagName;
    public string CylinderWorkInterlockTagName => Shell.CylinderWorkInterlockTagName;
    public string CylinderAlarmTimeSetting
    {
        get => Shell.CylinderAlarmTimeSetting;
        set => Shell.CylinderAlarmTimeSetting = value;
    }
    public string CylinderHomeDelaySetting
    {
        get => Shell.CylinderHomeDelaySetting;
        set => Shell.CylinderHomeDelaySetting = value;
    }
    public string CylinderWorkDelaySetting
    {
        get => Shell.CylinderWorkDelaySetting;
        set => Shell.CylinderWorkDelaySetting = value;
    }
    public string CylinderCurrentActionTimeDisplay => Shell.CylinderCurrentActionTimeDisplay;
    public string CylinderLastActionTimeDisplay => Shell.CylinderLastActionTimeDisplay;
    public int CylinderActionCount => Shell.CylinderActionCount;

    // -- Axis settings --
    public string AxisJogDistance
    {
        get => Shell.AxisJogDistance;
        set => Shell.AxisJogDistance = value;
    }
    public string AxisTargetPosition
    {
        get => Shell.AxisTargetPosition;
        set => Shell.AxisTargetPosition = value;
    }
}
