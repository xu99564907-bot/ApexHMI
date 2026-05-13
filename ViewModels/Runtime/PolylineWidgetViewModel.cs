using ApexHMI.Models.RuntimeUi;
using ApexHMI.Services.RuntimeUi;

namespace ApexHMI.ViewModels.Runtime;

/// <summary>折线控件。points 为像素坐标字符串 "x1,y1 x2,y2 ..."。</summary>
public partial class PolylineWidgetViewModel : WidgetViewModelBase
{
    public PolylineWidgetViewModel(WidgetInstance model, IWidgetDataContext dataContext)
        : base(model, dataContext) { }

    public string Points           => Prop("points",          "0,0 60,40 120,0");
    public string Stroke           => Prop("stroke",          "#1F2937");
    public string StrokeThickness  => Prop("strokeThickness", "2");
    public string StrokeDashArray  => Prop("strokeDashArray", "");
    public string Opacity          => Prop("opacity",         "1");
}
