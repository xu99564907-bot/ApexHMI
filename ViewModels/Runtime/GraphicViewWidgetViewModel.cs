using ApexHMI.Models.RuntimeUi;
using ApexHMI.Services.RuntimeUi;

namespace ApexHMI.ViewModels.Runtime;

/// <summary>图形视图控件（本地图片路径）。P6 引入资源管理。</summary>
public partial class GraphicViewWidgetViewModel : WidgetViewModelBase
{
    public GraphicViewWidgetViewModel(WidgetInstance model, IWidgetDataContext dataContext)
        : base(model, dataContext) { }

    public string Source  => Prop("source",  "");
    public string Stretch => Prop("stretch", "Uniform");
    public string Opacity => Prop("opacity", "1");

    /// <summary>P6D: MaterialDesignIcon Kind 名（如 "Valve"/"Pump"），非空时覆盖 Image 显示工业符号。</summary>
    public string IconKind => Prop("iconKind", "");

    /// <summary>P6D: 符号填充色（默认深蓝）。</summary>
    public string IconColor => Prop("iconColor", "#1E40AF");

    public bool ShowIcon => !string.IsNullOrEmpty(IconKind);
    public bool ShowImage => !ShowIcon && !string.IsNullOrEmpty(Source);
}
