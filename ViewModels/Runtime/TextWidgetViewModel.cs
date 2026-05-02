using CommunityToolkit.Mvvm.ComponentModel;
using ApexHMI.Models.RuntimeUi;
using ApexHMI.Services.RuntimeUi;

namespace ApexHMI.ViewModels.Runtime;

/// <summary>静态文本控件（不绑定 OPC UA）。</summary>
public partial class TextWidgetViewModel : WidgetViewModelBase
{
    public TextWidgetViewModel(WidgetInstance model, IWidgetDataContext dataContext)
        : base(model, dataContext) { }

    public string Text       => Prop("text",       "");
    public string FontSize   => Prop("fontSize",   "14");
    public string Foreground => Prop("foreground", "#1F2937");
    public string FontWeight => Prop("fontWeight", "Normal");
    public string TextAlignment => Prop("textAlign", "Left");
    public string Background => Prop("background", "Transparent");
}
