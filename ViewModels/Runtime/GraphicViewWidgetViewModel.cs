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
}
