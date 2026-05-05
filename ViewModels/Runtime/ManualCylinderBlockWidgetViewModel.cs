using System.Linq;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ApexHMI.Models;
using ApexHMI.Models.RuntimeUi;
using ApexHMI.Services.RuntimeUi;
using ApexHMI.ViewModels;

namespace ApexHMI.ViewModels.Runtime;

/// <summary>
/// 手动气缸块 widget：复用 Tab 3「手动操作」页气缸卡片的完整 UI 与行为。
/// 通过 Properties["deviceName"] 指定要显示的具体气缸（如 "Cyl1" 或显示名）。
/// 设计模式与运行模式都从 Shell.ManualCylinderBlockCards 取真实 ManualCylinderBlockItem。
/// </summary>
public partial class ManualCylinderBlockWidgetViewModel : WidgetViewModelBase
{
    private readonly MainViewModel? _shell;

    public ManualCylinderBlockWidgetViewModel(WidgetInstance model, IWidgetDataContext dataContext)
        : base(model, dataContext)
    {
        _shell = dataContext.Shell as MainViewModel;
        Block = ResolveBlock();
    }

    /// <summary>实际气缸数据项；找不到则为 null（widget 显示占位）。</summary>
    public ManualCylinderBlockItem? Block { get; }

    /// <summary>是否成功绑定到具体气缸。</summary>
    public bool HasBlock => Block is not null;

    /// <summary>未绑定时的提示文本。</summary>
    public string PlaceholderText
    {
        get
        {
            var name = Prop("deviceName", string.Empty);
            return string.IsNullOrWhiteSpace(name)
                ? "气缸块（未指定设备）"
                : $"未找到气缸：{name}";
        }
    }

    private ManualCylinderBlockItem? ResolveBlock()
    {
        if (_shell is null) return null;
        var name = Prop("deviceName", string.Empty);
        if (string.IsNullOrWhiteSpace(name)) return null;

        return _shell.ManualCylinderBlockCards.FirstOrDefault(c =>
            string.Equals(c.DisplayName, name, System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals($"Cyl{c.CylinderIndex}", name, System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(c.CylinderIndex.ToString(), name, System.StringComparison.Ordinal));
    }

    /// <summary>转发 Tab 3 的"工作位指令"。</summary>
    public ICommand? WorkCommand => _shell?.CylinderMoveToWorkCommand;

    /// <summary>转发 Tab 3 的"原位指令"。</summary>
    public ICommand? HomeCommand => _shell?.CylinderMoveToHomeCommand;
}
