using System.Globalization;
using ApexHMI.Models.RuntimeUi;
using ApexHMI.Services.RuntimeUi;

namespace ApexHMI.ViewModels.Runtime;

/// <summary>直线控件。X1/Y1/X2/Y2 用 0-1 相对坐标（默认对角线 0,0 → 1,1）。</summary>
public partial class LineWidgetViewModel : WidgetViewModelBase
{
    public LineWidgetViewModel(WidgetInstance model, IWidgetDataContext dataContext)
        : base(model, dataContext) { }

    public string Stroke           => Prop("stroke",          "#1F2937");
    public string StrokeThickness  => Prop("strokeThickness", "2");
    /// <summary>实线/虚线/点线：空=实线，"4 2"=虚线，"1 2"=点线。</summary>
    public string StrokeDashArray  => Prop("strokeDashArray", "");

    public double X1Px => GetRel("x1", 0) * Model.Width;
    public double Y1Px => GetRel("y1", 0) * Model.Height;
    public double X2Px => GetRel("x2", 1) * Model.Width;
    public double Y2Px => GetRel("y2", 1) * Model.Height;

    private double GetRel(string key, double fallback)
    {
        var s = Prop(key, fallback.ToString(CultureInfo.InvariantCulture));
        return double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : fallback;
    }
}
