using ApexHMI.Models.RuntimeUi;
using ApexHMI.Services.RuntimeUi;

namespace ApexHMI.ViewModels.Runtime;

/// <summary>椭圆/圆控件（宽高相等即为圆）。</summary>
public partial class EllipseWidgetViewModel : WidgetViewModelBase
{
    public EllipseWidgetViewModel(WidgetInstance model, IWidgetDataContext dataContext)
        : base(model, dataContext) { }

    public string Fill            => Prop("fill",            "#10B981");
    public string Stroke          => Prop("stroke",          "#065F46");
    public string StrokeThickness => Prop("strokeThickness", "1");
    public string Opacity         => Prop("opacity",         "1");
}
