using ApexHMI.Models.RuntimeUi;
using ApexHMI.Services.RuntimeUi;
using ApexHMI.ViewModels;

namespace ApexHMI.ViewModels.Runtime;

/// <summary>
/// 手动机械手 widget：复用 Tab 3 的 RobotControl + RobotControlViewModel。
/// 当前阶段单实例（绑定 Shell.RobotControlViewModel）；多机械手支持留待后续。
/// </summary>
public partial class ManualRobotBlockWidgetViewModel : WidgetViewModelBase
{
    public ManualRobotBlockWidgetViewModel(WidgetInstance model, IWidgetDataContext dataContext)
        : base(model, dataContext)
    {
        var shell = dataContext.Shell as MainViewModel;
        RobotVm = shell?.RobotControlViewModel;
    }

    public RobotControlViewModel? RobotVm { get; }
    public bool HasRobot => RobotVm is not null;
}
