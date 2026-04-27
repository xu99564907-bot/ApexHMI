using CommunityToolkit.Mvvm.ComponentModel;
using ApexHMI.Models;

namespace ApexHMI.ViewModels;

/// <summary>
/// 机械手演示页面ViewModel
/// </summary>
public partial class RobotDemoViewModel : ObservableObject
{
    [ObservableProperty]
    private RobotControlViewModel _robot1;

    [ObservableProperty]
    private RobotControlViewModel _robot2;

    [ObservableProperty]
    private RobotControlViewModel _robot3;

    public RobotDemoViewModel()
    {
        // 机械手1 - 就绪状态
        Robot1 = new RobotControlViewModel(1, "EPSON机械手 #1");
        Robot1.Robot.Status.Ready = true;
        Robot1.Robot.Status.MotorsOn = true;
        Robot1.Robot.Status.PowerHigh = true;
        Robot1.Robot.Status.AtHome = true;
        Robot1.Robot.Command.Speed = 50;
        Robot1.Robot.Command.PointNum = 0;
        Robot1.Robot.Command.ProductType = 1;
        Robot1.Robot.CurrentX = 0.00;
        Robot1.Robot.CurrentY = 0.00;
        Robot1.Robot.CurrentZ = 0.00;

        // 机械手2 - 运行状态
        Robot2 = new RobotControlViewModel(2, "EPSON机械手 #2");
        Robot2.Robot.Status.Ready = false;
        Robot2.Robot.Status.Running = true;
        Robot2.Robot.Status.MotorsOn = true;
        Robot2.Robot.Status.PowerHigh = true;
        Robot2.Robot.Command.Speed = 75;
        Robot2.Robot.Command.PointNum = 5;
        Robot2.Robot.Command.ProductType = 2;
        Robot2.Robot.CurrentX = 125.50;
        Robot2.Robot.CurrentY = 80.25;
        Robot2.Robot.CurrentZ = 45.00;

        // 机械手3 - 错误状态
        Robot3 = new RobotControlViewModel(3, "EPSON机械手 #3");
        Robot3.Robot.Status.Error = true;
        Robot3.Robot.Status.ErrorCode = 0x4223;
        Robot3.Robot.Status.Ready = false;
        Robot3.Robot.Status.MotorsOn = false;
        Robot3.Robot.Command.Speed = 0;
        Robot3.Robot.Command.PointNum = 0;
        Robot3.Robot.Command.ProductType = 1;
        Robot3.Robot.CurrentX = 0;
        Robot3.Robot.CurrentY = 0;
        Robot3.Robot.CurrentZ = 0;
    }
}
