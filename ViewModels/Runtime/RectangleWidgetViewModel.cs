using ApexHMI.Models.RuntimeUi;
using ApexHMI.Services.RuntimeUi;

namespace ApexHMI.ViewModels.Runtime;

/// <summary>矩形控件（圆角/填充/边框/透明度）。</summary>
public partial class RectangleWidgetViewModel : WidgetViewModelBase
{
    public RectangleWidgetViewModel(WidgetInstance model, IWidgetDataContext dataContext)
        : base(model, dataContext) { }

    public string Fill            => Prop("fill",            "#3B82F6");
    public string Stroke          => Prop("stroke",          "#1E40AF");
    public string StrokeThickness => Prop("strokeThickness", "1");
    public string CornerRadius    => Prop("cornerRadius",    "0");
    public string Opacity         => Prop("opacity",         "1");
}
