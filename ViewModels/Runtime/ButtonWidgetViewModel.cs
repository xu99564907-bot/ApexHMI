using System;
using CommunityToolkit.Mvvm.Input;
using ApexHMI.Models.RuntimeUi;
using ApexHMI.Services.RuntimeUi;

namespace ApexHMI.ViewModels.Runtime;

/// <summary>
/// 按钮控件：除原有的 write-bool / write-pulse / navigate 外，新增四种简化按钮模式：
/// <list type="bullet">
///   <item><c>set-on</c> — 单击写 True</item>
///   <item><c>set-off</c> — 单击写 False</item>
///   <item><c>toggle</c> — 单击读当前值后写反值（读地址：Binding.TagId，留空则用 ActionParam）</item>
///   <item><c>momentary</c> — 按下写 True、松开写 False（点动 / 复归型）</item>
/// </list>
/// 写入地址 = <c>Model.ActionParam</c>；读取地址 = <c>Model.Binding.TagId</c>（留空 fallback 用 ActionParam）。
/// </summary>
public partial class ButtonWidgetViewModel : WidgetViewModelBase
{
    private bool _currentBoolValue;

    public ButtonWidgetViewModel(WidgetInstance model, IWidgetDataContext dataContext)
        : base(model, dataContext)
    {
        var readTag = ResolveReadTag();
        if (!string.IsNullOrEmpty(readTag))
        {
            _dataContext.RegisterValueCallback(readTag!, v => _currentBoolValue = ParseBool(v));
        }
    }

    public string Text       => Prop("text",       "按钮");
    public string Background => Prop("background", "#2563EB");
    public string Foreground => Prop("foreground", "#FFFFFF");

    private string? ResolveReadTag()
    {
        var t = Model.Binding?.TagId;
        if (!string.IsNullOrWhiteSpace(t)) return t;
        if (IsButtonMode(Model.ActionType) && !string.IsNullOrWhiteSpace(Model.ActionParam))
            return Model.ActionParam;
        return null;
    }

    private static bool IsButtonMode(string? at) =>
        at is "set-on" or "set-off" or "toggle" or "momentary";

    private static bool ParseBool(string s) =>
        !string.IsNullOrEmpty(s) &&
        (string.Equals(s, "True", StringComparison.OrdinalIgnoreCase) || s == "1");

    private bool CheckPermission()
    {
        if (string.IsNullOrWhiteSpace(Model.RequiredRole) ||
            _dataContext.Shell is not ApexHMI.ViewModels.MainViewModel shell) return true;
        if (!System.Enum.TryParse<ApexHMI.Models.UserRole>(Model.RequiredRole, true, out var required))
            required = ApexHMI.Models.UserRole.Operator;
        if (shell.CurrentUserRole < required)
        {
            shell.SystemMessage = $"权限不足：操作需要 {required} 角色";
            return false;
        }
        return true;
    }

    [RelayCommand]
    private void Click()
    {
        if (!CheckPermission()) return;

        var at = Model.ActionType ?? string.Empty;
        var writeTag = Model.ActionParam ?? string.Empty;

        switch (at)
        {
            case "set-on":
                if (!string.IsNullOrWhiteSpace(writeTag))
                    _dataContext.ExecuteAction("write-bool", $"{writeTag}|True");
                break;
            case "set-off":
                if (!string.IsNullOrWhiteSpace(writeTag))
                    _dataContext.ExecuteAction("write-bool", $"{writeTag}|False");
                break;
            case "toggle":
                if (!string.IsNullOrWhiteSpace(writeTag))
                    _dataContext.ExecuteAction("write-bool", $"{writeTag}|{!_currentBoolValue}");
                break;
            case "momentary":
                // 由 PressDown / Release 处理；Click 留空
                break;
            default:
                if (!string.IsNullOrEmpty(at))
                    _dataContext.ExecuteAction(at, writeTag);
                break;
        }
    }

    /// <summary>复归型按下：写 True。其他模式忽略。</summary>
    [RelayCommand]
    private void PressDown()
    {
        if (Model.ActionType != "momentary") return;
        if (!CheckPermission()) return;
        var writeTag = Model.ActionParam ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(writeTag))
            _dataContext.ExecuteAction("write-bool", $"{writeTag}|True");
    }

    /// <summary>复归型松开：写 False。其他模式忽略；无权限校验（保证一定能恢复）。</summary>
    [RelayCommand]
    private void Release()
    {
        if (Model.ActionType != "momentary") return;
        var writeTag = Model.ActionParam ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(writeTag))
            _dataContext.ExecuteAction("write-bool", $"{writeTag}|False");
    }
}
