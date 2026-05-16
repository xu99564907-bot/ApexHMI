using CommunityToolkit.Mvvm.ComponentModel;
using ApexHMI.Models.RuntimeUi;
using ApexHMI.Services.RuntimeUi;

namespace ApexHMI.ViewModels.Runtime;

/// <summary>
/// 静态文本控件（不绑定 OPC UA）。
/// P3 升级：支持字号/字重/前景背景/水平垂直对齐/边距。
/// </summary>
public partial class TextWidgetViewModel : WidgetViewModelBase
{
    public TextWidgetViewModel(WidgetInstance model, IWidgetDataContext dataContext)
        : base(model, dataContext) { }

    public string Text               => Prop("text",       "");
    public string FontSize           => Prop("fontSize",   "14");
    public string FontWeight         => Prop("fontWeight", "Normal");
    public string Foreground         => Prop("foreground", "#1F2937");
    public string Background         => Prop("background", "Transparent");
    public string TextAlignment      => Prop("textAlign",  "Left");
    public string VerticalAlignment  => Prop("verticalAlign", "Center");
    public string Padding            => Prop("padding",    "4");
}
