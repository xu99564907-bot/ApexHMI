using System.Linq;
using System.Windows.Input;
using ApexHMI.Models.RuntimeUi;
using ApexHMI.Services.RuntimeUi;
using ApexHMI.ViewModels;

namespace ApexHMI.ViewModels.Runtime;

/// <summary>
/// 手动挡停 widget：通过按钮切换挡停 Tag (Stopper_Up 等)。
/// Properties["deviceName"] 为目标 Tag 名（默认 Stopper_Up）。
/// 状态 Tag 名通过 Properties["statusTagName"]（可选，默认与 deviceName 相同）。
/// </summary>
public partial class ManualStopperBlockWidgetViewModel : WidgetViewModelBase
{
    private readonly MainViewModel? _shell;

    public ManualStopperBlockWidgetViewModel(WidgetInstance model, IWidgetDataContext dataContext)
        : base(model, dataContext)
    {
        _shell = dataContext.Shell as MainViewModel;
    }

    public string TagName => Prop("deviceName", "Stopper_Up");

    public string DisplayName
    {
        get
        {
            var name = Prop("displayName", string.Empty);
            return string.IsNullOrWhiteSpace(name) ? $"挡停 {TagName}" : name;
        }
    }

    /// <summary>当前挡停状态：从 Shell.Tags 实时取值（无则显示 "?"）。</summary>
    public string CurrentValueText
    {
        get
        {
            var tag = _shell?.Tags.FirstOrDefault(t =>
                string.Equals(t.Name, TagName, System.StringComparison.OrdinalIgnoreCase));
            return tag?.CurrentValue ?? "?";
        }
    }

    public ICommand? ToggleCommand => _shell?.ToggleDeviceCommand;
}
