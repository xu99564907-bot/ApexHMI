using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ApexHMI.Models;
using System.Windows;

namespace ApexHMI.ViewModels;

/// <summary>
/// 机械手控制ViewModel
/// </summary>
public partial class RobotControlViewModel : ObservableObject
{
    [ObservableProperty]
    private RobotModel _robot = new();

    [ObservableProperty]
    private bool _isEditing;

    public RobotControlViewModel()
    {
        Robot.RobotId = 1;
        Robot.RobotName = "EPSON机械手 #1";
    }

    public RobotControlViewModel(int robotId, string robotName)
    {
        Robot.RobotId = robotId;
        Robot.RobotName = robotName;
    }

    #region 主控制命令

    [RelayCommand]
    private void MotorOn()
    {
        Robot.Command.SetMotorsOn = true;
        // TODO: 发送到PLC
        ShowMessage("电机启动命令已发送");
    }

    [RelayCommand]
    private void MotorOff()
    {
        Robot.Command.SetMotorsOff = true;
        // TODO: 发送到PLC
        ShowMessage("电机关闭命令已发送");
    }

    [RelayCommand]
    private void Start()
    {
        if (!Robot.Status.Ready && !Robot.Status.Paused)
        {
            ShowMessage("机械手未就绪", MessageBoxImage.Warning);
            return;
        }
        Robot.Command.RobStart = true;
        ShowMessage("启动命令已发送");
    }

    [RelayCommand]
    private void Stop()
    {
        Robot.Command.RobStop = true;
        ShowMessage("停止命令已发送");
    }

    [RelayCommand]
    private void Pause()
    {
        if (!Robot.Status.Running)
        {
            ShowMessage("机械手未运行", MessageBoxImage.Warning);
            return;
        }
        Robot.Command.RobPause = true;
        ShowMessage("暂停命令已发送");
    }

    [RelayCommand]
    private void Continue()
    {
        if (!Robot.Status.Paused)
        {
            ShowMessage("机械手未暂停", MessageBoxImage.Warning);
            return;
        }
        Robot.Command.RobContinue = true;
        ShowMessage("继续命令已发送");
    }

    [RelayCommand]
    private void Reset()
    {
        Robot.Command.RobReset = true;
        ShowMessage("复位命令已发送");
    }

    [RelayCommand]
    private void GoHome()
    {
        Robot.Command.MachineIntial = true;
        ShowMessage("回原点命令已发送");
    }

    [RelayCommand]
    private void SetPowerHigh()
    {
        Robot.Command.SetPowerHigh = true;
        ShowMessage("高功率模式已设置");
    }

    [RelayCommand]
    private void SetPowerLow()
    {
        Robot.Command.SetPowerLow = true;
        ShowMessage("低功率模式已设置");
    }

    [RelayCommand]
    private void SFree()
    {
        Robot.Command.SFree = true;
        ShowMessage("释放刹车命令已发送");
    }

    [RelayCommand]
    private void SLock()
    {
        Robot.Command.SLock = true;
        ShowMessage("锁定刹车命令已发送");
    }

    #endregion

    #region 轴点动命令

    [RelayCommand]
    private void JogXForward()
    {
        Robot.Command.XFor = true;
    }

    [RelayCommand]
    private void JogXBackward()
    {
        Robot.Command.XBack = true;
    }

    [RelayCommand]
    private void JogYForward()
    {
        Robot.Command.YFor = true;
    }

    [RelayCommand]
    private void JogYBackward()
    {
        Robot.Command.YBack = true;
    }

    [RelayCommand]
    private void JogZForward()
    {
        Robot.Command.ZFor = true;
    }

    [RelayCommand]
    private void JogZBackward()
    {
        Robot.Command.ZBack = true;
    }

    [RelayCommand]
    private void JogUForward()
    {
        Robot.Command.UFor = true;
    }

    [RelayCommand]
    private void JogUBackward()
    {
        Robot.Command.UBack = true;
    }

    [RelayCommand]
    private void JogVForward()
    {
        Robot.Command.VFor = true;
    }

    [RelayCommand]
    private void JogVBackward()
    {
        Robot.Command.VBack = true;
    }

    [RelayCommand]
    private void JogWForward()
    {
        Robot.Command.WFor = true;
    }

    [RelayCommand]
    private void JogWBackward()
    {
        Robot.Command.WBack = true;
    }

    #endregion

    #region 点位操作命令

    [RelayCommand]
    private void SavePoint()
    {
        Robot.Command.SavePoint = true;
        ShowMessage($"点位 P{Robot.Command.PointNum} 保存命令已发送");
    }

    [RelayCommand]
    private void JumpToPoint()
    {
        Robot.Command.RobJump = true;
        ShowMessage($"跳转到点位 P{Robot.Command.PointNum} 命令已发送");
    }

    #endregion

    #region 模式切换

    [RelayCommand]
    private void SetManualMode()
    {
        Robot.Command.ManuMode = true;
        Robot.Command.AutoMode = false;
        ShowMessage("切换到手动模式");
    }

    [RelayCommand]
    private void SetAutoMode()
    {
        Robot.Command.AutoMode = true;
        Robot.Command.ManuMode = false;
        ShowMessage("切换到自动模式");
    }

    [RelayCommand]
    private void SetTeachMode()
    {
        Robot.Command.TeachMode = !Robot.Command.TeachMode;
        ShowMessage(Robot.Command.TeachMode ? "示教模式已开启" : "示教模式已关闭");
    }

    #endregion

    private void ShowMessage(string message, MessageBoxImage icon = MessageBoxImage.Information)
    {
        // 实际项目中可以改用消息通知服务
        MessageBox.Show(message, "机械手控制", MessageBoxButton.OK, icon);
    }
}
