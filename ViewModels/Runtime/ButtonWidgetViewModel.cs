using CommunityToolkit.Mvvm.Input;
using ApexHMI.Models.RuntimeUi;
using ApexHMI.Services.RuntimeUi;

namespace ApexHMI.ViewModels.Runtime;

/// <summary>按钮控件：支持 write-bool / write-pulse / navigate 动作。</summary>
public partial class ButtonWidgetViewModel : WidgetViewModelBase
{
    public ButtonWidgetViewModel(WidgetInstance model, IWidgetDataContext dataContext)
        : base(model, dataContext) { }

    public string Text       => Prop("text",       "按钮");
    public string Background => Prop("background", "#2563EB");
    public string Foreground => Prop("foreground", "#FFFFFF");

    [RelayCommand]
    private void Click()
    {
        if (!string.IsNullOrEmpty(Model.ActionType))
        {
            _dataContext.ExecuteAction(Model.ActionType, Model.ActionParam ?? string.Empty);
        }
    }
}
