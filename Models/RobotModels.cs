using CommunityToolkit.Mvvm.ComponentModel;

namespace ApexHMI.Models;

/// <summary>
/// EPSON机械手命令结构体 - 对应PLC的Str_EpsonCmd
/// </summary>
public partial class RobotCommand : ObservableObject
{
    // 基本控制
    [ObservableProperty]
    private bool _robStart;

    [ObservableProperty]
    private bool _robStop;

    [ObservableProperty]
    private bool _robPause;

    [ObservableProperty]
    private bool _robContinue;

    [ObservableProperty]
    private bool _robReset;

    // 电机控制
    [ObservableProperty]
    private bool _setMotorsOn;

    [ObservableProperty]
    private bool _setMotorsOff;

    // 功率控制
    [ObservableProperty]
    private bool _setPowerHigh;

    [ObservableProperty]
    private bool _setPowerLow;

    // 轴点动控制
    [ObservableProperty]
    private bool _xFor;

    [ObservableProperty]
    private bool _xBack;

    [ObservableProperty]
    private bool _yFor;

    [ObservableProperty]
    private bool _yBack;

    [ObservableProperty]
    private bool _zFor;

    [ObservableProperty]
    private bool _zBack;

    [ObservableProperty]
    private bool _uFor;

    [ObservableProperty]
    private bool _uBack;

    [ObservableProperty]
    private bool _vFor;

    [ObservableProperty]
    private bool _vBack;

    [ObservableProperty]
    private bool _wFor;

    [ObservableProperty]
    private bool _wBack;

    // 点位控制
    [ObservableProperty]
    private bool _savePoint;

    [ObservableProperty]
    private bool _robJump;

    // 模式控制
    [ObservableProperty]
    private bool _manuMode;

    [ObservableProperty]
    private bool _autoMode;

    [ObservableProperty]
    private bool _sFree;

    [ObservableProperty]
    private bool _sLock;

    [ObservableProperty]
    private bool _teachMode;

    // 机器控制
    [ObservableProperty]
    private bool _machineIntial;

    [ObservableProperty]
    private bool _machineAutoRun;

    [ObservableProperty]
    private bool _machineStepRun;

    // 参数
    [ObservableProperty]
    private short _speed;

    [ObservableProperty]
    private short _pointNum;

    [ObservableProperty]
    private short _productType;
}

/// <summary>
/// EPSON机械手状态结构体 - 对应PLC的Str_EpsonStatus
/// </summary>
public partial class RobotStatus : ObservableObject
{
    // 基本状态
    [ObservableProperty]
    private bool _ready;

    [ObservableProperty]
    private bool _running;

    [ObservableProperty]
    private bool _paused;

    [ObservableProperty]
    private bool _error;

    [ObservableProperty]
    private bool _eStopOn;

    [ObservableProperty]
    private bool _safeguardOn;

    [ObservableProperty]
    private bool _sError;

    [ObservableProperty]
    private bool _warning;

    // 电机和功率状态
    [ObservableProperty]
    private bool _motorsOn;

    [ObservableProperty]
    private bool _atHome;

    [ObservableProperty]
    private bool _powerHigh;

    [ObservableProperty]
    private bool _resetOver;

    [ObservableProperty]
    private bool _free;

    [ObservableProperty]
    private bool _lock;

    [ObservableProperty]
    private bool _teachOver;

    [ObservableProperty]
    private bool _intialed;

    // 错误代码
    [ObservableProperty]
    private ushort _errorCode;

    // 料盒状态 (InsideBox1-15)
    [ObservableProperty]
    private bool[] _insideBoxes = new bool[15];

    // 平面状态 (InsidePlane1-15)
    [ObservableProperty]
    private bool[] _insidePlanes = new bool[15];
}

/// <summary>
/// EPSON机械手完整数据模型
/// </summary>
public partial class RobotModel : ObservableObject
{
    [ObservableProperty]
    private int _robotId;

    [ObservableProperty]
    private string _robotName = $"EPSON机械手";

    [ObservableProperty]
    private RobotCommand _command = new();

    [ObservableProperty]
    private RobotStatus _status = new();

    [ObservableProperty]
    private double _currentX;

    [ObservableProperty]
    private double _currentY;

    [ObservableProperty]
    private double _currentZ;

    [ObservableProperty]
    private double _currentU;

    [ObservableProperty]
    private double _currentV;

    [ObservableProperty]
    private double _currentW;

    /// <summary>
    /// 获取机械手运行状态文本
    /// </summary>
    public string StateText
    {
        get
        {
            if (Status.Error) return "ERROR";
            if (Status.EStopOn) return "ESTOP";
            if (Status.Paused) return "PAUSED";
            if (Status.Running) return "RUNNING";
            if (Status.Ready) return "READY";
            return "OFFLINE";
        }
    }

    /// <summary>
    /// 获取状态颜色
    /// </summary>
    public string StateColor
    {
        get
        {
            if (Status.Error) return "#DC2626";
            if (Status.EStopOn) return "#DC2626";
            if (Status.Paused) return "#F59E0B";
            if (Status.Running) return "#2563EB";
            if (Status.Ready) return "#10B981";
            return "#6B7280";
        }
    }
}

/// <summary>
/// 机械手状态枚举
/// </summary>
public enum RobotState
{
    Offline,
    Ready,
    Running,
    Paused,
    Error,
    EStop
}
