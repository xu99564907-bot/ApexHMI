using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ApexHMI.Models;

public partial class ManualAxisBlockItem : ObservableObject
{
    [ObservableProperty] private int axisIndex;
    [ObservableProperty] private int displayOrder;
    [ObservableProperty] private string displayName = string.Empty;

    // Command bindings
    [ObservableProperty] private string powerCommandTagName = string.Empty;
    [ObservableProperty] private string stopCommandTagName = string.Empty;
    [ObservableProperty] private string manuToHomeTagName = string.Empty;
    [ObservableProperty] private string manuJogForwardTagName = string.Empty;
    [ObservableProperty] private string manuJogBackwardTagName = string.Empty;
    [ObservableProperty] private string teachOnTagName = string.Empty;
    [ObservableProperty] private string teachTagName = string.Empty;
    [ObservableProperty] private string manuPointTagName = string.Empty;
    [ObservableProperty] private string pointSelectTagName = string.Empty;
    [ObservableProperty] private string autoAbsTagName = string.Empty;
    [ObservableProperty] private string manuPositionTagName = string.Empty;
    [ObservableProperty] private string velocityControlTagName = string.Empty;

    // DevStatus bindings
    [ObservableProperty] private string homeSignalTagName = string.Empty;
    [ObservableProperty] private string positiveLimitTagName = string.Empty;
    [ObservableProperty] private string negativeLimitTagName = string.Empty;
    [ObservableProperty] private string alarmSignalTagName = string.Empty;
    [ObservableProperty] private string servoEnableFbTagName = string.Empty;
    [ObservableProperty] private string resetFbTagName = string.Empty;
    [ObservableProperty] private string brakeStatusTagName = string.Empty;

    // Status bindings
    [ObservableProperty] private string powerOnTagName = string.Empty;
    [ObservableProperty] private string busyTagName = string.Empty;
    [ObservableProperty] private string posOkTagName = string.Empty;
    [ObservableProperty] private string initializedTagName = string.Empty;
    [ObservableProperty] private string errorTagName = string.Empty;
    [ObservableProperty] private string errorIdTagName = string.Empty;
    [ObservableProperty] private string actualPositionTagName = string.Empty;
    [ObservableProperty] private string actualVelocityTagName = string.Empty;
    [ObservableProperty] private string actualTorqueTagName = string.Empty;
    [ObservableProperty] private string stopPositionTagName2 = string.Empty;
    [ObservableProperty] private string hmiPositionTagName = string.Empty;
    [ObservableProperty] private string stateTagName = string.Empty;
    [ObservableProperty] private string pausedTagName = string.Empty;

    // Parm bindings
    [ObservableProperty] private string homeInterlockTagName = string.Empty;
    [ObservableProperty] private string jogInterlockTagName = string.Empty;
    [ObservableProperty] private string positioningInterlockTagName = string.Empty;
    [ObservableProperty] private string setPositionTagName = string.Empty;
    [ObservableProperty] private string setVelocityTagName = string.Empty;
    [ObservableProperty] private string stopPositionTagName = string.Empty;

    // Runtime states
    [ObservableProperty] private bool homeSignalActive;
    [ObservableProperty] private bool positiveLimitActive;
    [ObservableProperty] private bool negativeLimitActive;
    [ObservableProperty] private bool alarmActive;
    [ObservableProperty] private bool servoEnabledFeedback;
    [ObservableProperty] private bool motorRunning;
    [ObservableProperty] private bool motorActionDone;
    [ObservableProperty] private bool homingComplete;
    [ObservableProperty] private bool motorError;
    [ObservableProperty] private bool brakeActive;
    [ObservableProperty] private string errorIdText = "0";
    [ObservableProperty] private string actualPositionDisplay = "0.000";
    [ObservableProperty] private string actualVelocityDisplay = "0.0";
    [ObservableProperty] private string actualTorqueDisplay = "0.0";
    [ObservableProperty] private string stopPositionDisplay = "0.000";
    [ObservableProperty] private string hmiPositionDisplay = "0.000";
    [ObservableProperty] private int stateCode;
    [ObservableProperty] private string stateCodeText = "0: Power_off";

    // Inputs
    [ObservableProperty] private string setPositionInput = "0.000";
    [ObservableProperty] private string setVelocityInput = "20";
    [ObservableProperty] private int selectedPointIndex;

    // Point position options (从 IO 配置表"轴名称"Sheet 加载)
    public ObservableCollection<AxisPointLabel> PointOptions { get; } = new();

    // Interlocks and hints
    [ObservableProperty] private bool homeInterlock = true;
    [ObservableProperty] private bool jogInterlock = true;
    [ObservableProperty] private bool positioningInterlock = true;
    [ObservableProperty] private string currentStateText = "未连接";
    [ObservableProperty] private string interlockHint = string.Empty;
    [ObservableProperty] private string statusText = "待机";
}
