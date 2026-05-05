using System.Linq;
using System.Windows.Input;
using ApexHMI.Models;
using ApexHMI.Models.RuntimeUi;
using ApexHMI.Services.RuntimeUi;
using ApexHMI.ViewModels;

namespace ApexHMI.ViewModels.Runtime;

/// <summary>
/// 手动轴块 widget：复用 Tab 3 手动操作页轴卡片的核心字段与命令。
/// 通过 Properties["deviceName"] 指定具体轴（如 "Axis1" 或显示名）。
/// </summary>
public partial class ManualAxisBlockWidgetViewModel : WidgetViewModelBase
{
    private readonly MainViewModel? _shell;

    public ManualAxisBlockWidgetViewModel(WidgetInstance model, IWidgetDataContext dataContext)
        : base(model, dataContext)
    {
        _shell = dataContext.Shell as MainViewModel;
        Block = ResolveBlock();
    }

    public ManualAxisBlockItem? Block { get; }
    public bool HasBlock => Block is not null;

    public string PlaceholderText
    {
        get
        {
            var name = Prop("deviceName", string.Empty);
            return string.IsNullOrWhiteSpace(name)
                ? "轴块（未指定设备）"
                : $"未找到轴：{name}";
        }
    }

    private ManualAxisBlockItem? ResolveBlock()
    {
        if (_shell is null) return null;
        var name = Prop("deviceName", string.Empty);
        if (string.IsNullOrWhiteSpace(name)) return null;

        return _shell.ManualAxisBlockCards.FirstOrDefault(a =>
            string.Equals(a.DisplayName, name, System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals($"Axis{a.AxisIndex}", name, System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(a.AxisIndex.ToString(), name, System.StringComparison.Ordinal));
    }

    // 转发 Tab 3 的轴常用命令（块级，参数为 ManualAxisBlockItem）
    public ICommand? ToggleEnableCommand => _shell?.AxisToggleEnableCommand;
    public ICommand? StopCommand         => _shell?.AxisStopCommand;
    public ICommand? MoveToHomeCommand   => _shell?.AxisMoveToHomeCommand;
    public ICommand? JogForwardCommand   => _shell?.AxisJogForwardCommand;
    public ICommand? JogBackwardCommand  => _shell?.AxisJogBackwardCommand;
}
