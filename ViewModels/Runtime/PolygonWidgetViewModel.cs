using ApexHMI.Models.RuntimeUi;
using ApexHMI.Services.RuntimeUi;

namespace ApexHMI.ViewModels.Runtime;

/// <summary>多边形控件。points 为像素坐标字符串 "x1,y1 x2,y2 ..."。</summary>
public partial class PolygonWidgetViewModel : WidgetViewModelBase
{
    public PolygonWidgetViewModel(WidgetInstance model, IWidgetDataContext dataContext)
        : base(model, dataContext) { }

    public string Points           => Prop("points",          "60,0 120,60 60,120 0,60");
    public string Fill             => Prop("fill",            "#F59E0B");
    public string Stroke           => Prop("stroke",          "#92400E");
    public string StrokeThickness  => Prop("strokeThickness", "1");
    public string StrokeDashArray  => Prop("strokeDashArray", "");
    public string Opacity          => Prop("opacity",         "1");
}
