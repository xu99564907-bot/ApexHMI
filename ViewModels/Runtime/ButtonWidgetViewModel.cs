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
        // P3.3 控件级权限：RequiredRole 不为空时按 Shell.CurrentUserRole 校验
        if (!string.IsNullOrWhiteSpace(Model.RequiredRole) &&
            _dataContext.Shell is ApexHMI.ViewModels.MainViewModel shell)
        {
            if (!System.Enum.TryParse<ApexHMI.Models.UserRole>(Model.RequiredRole, true, out var required))
                required = ApexHMI.Models.UserRole.Operator;
            if (shell.CurrentUserRole < required)
            {
                shell.SystemMessage = $"权限不足：操作需要 {required} 角色";
                return;
            }
        }

        if (!string.IsNullOrEmpty(Model.ActionType))
        {
            _dataContext.ExecuteAction(Model.ActionType, Model.ActionParam ?? string.Empty);
        }
    }
}
