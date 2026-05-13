#nullable enable
using CommunityToolkit.Mvvm.ComponentModel;
using ApexHMI.Models.RuntimeUi;
using ApexHMI.Services.RuntimeUi;

namespace ApexHMI.ViewModels.Runtime;

/// <summary>
/// 复选框：绑 BOOL Tag，勾选写 True，取消勾选写 False。
/// </summary>
public partial class CheckBoxWidgetViewModel : WidgetViewModelBase
{
    [ObservableProperty] private bool _isChecked;
    private bool _initializingFromTag;

    public CheckBoxWidgetViewModel(WidgetInstance model, IWidgetDataContext dataContext)
        : base(model, dataContext)
    {
        var tag = ResolveTag();
        if (!string.IsNullOrWhiteSpace(tag))
            dataContext.RegisterValueCallback(tag!, v =>
            {
                _initializingFromTag = true;
                try { IsChecked = ParseBool(v); }
                finally { _initializingFromTag = false; }
            });
    }

    public string Text             => Prop("text",             "选项");
    public string CheckedColor     => Prop("checkedColor",     "#10B981");
    public string UncheckedColor   => Prop("uncheckedColor",   "#94A3B8");
    public string Foreground       => Prop("foreground",       "#0F172A");

    private string? ResolveTag()
    {
        var v = Prop("variable", "");
        if (!string.IsNullOrWhiteSpace(v)) return v;
        return Model.Binding?.TagId;
    }

    private static bool ParseBool(string s) =>
        !string.IsNullOrEmpty(s) && (string.Equals(s, "True", System.StringComparison.OrdinalIgnoreCase) || s == "1");

    partial void OnIsCheckedChanged(bool value)
    {
        if (_initializingFromTag) return;
        var tag = ResolveTag();
        if (string.IsNullOrWhiteSpace(tag)) return;
        _dataContext.ExecuteAction("write-bool", $"{tag}|{value}");
    }
}
