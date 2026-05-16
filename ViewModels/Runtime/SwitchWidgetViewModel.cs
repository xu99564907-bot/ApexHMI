#nullable enable
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ApexHMI.Models.RuntimeUi;
using ApexHMI.Services.RuntimeUi;

namespace ApexHMI.ViewModels.Runtime;

/// <summary>
/// 开关控件（Switch）：bistable=单击切换；momentary=按下 True、松开 False。
/// 绑定地址优先级：Properties["variable"] > Model.Binding.TagId。
/// </summary>
public partial class SwitchWidgetViewModel : WidgetViewModelBase
{
    [ObservableProperty] private bool _isOn;

    public SwitchWidgetViewModel(WidgetInstance model, IWidgetDataContext dataContext)
        : base(model, dataContext)
    {
        var tag = ResolveTag();
        if (!string.IsNullOrWhiteSpace(tag))
            dataContext.RegisterValueCallback(tag!, v => IsOn = ParseBool(v));
    }

    /// <summary>M4.4: Switch 总参与 Tab 焦点链（可点击切换）。</summary>
    public bool IsTabStop => true;

    public string ModeProp     => Prop("mode",        "bistable");
    public string OnText       => Prop("onText",      "ON");
    public string OffText      => Prop("offText",     "OFF");
    public string OnColor      => Prop("onColor",     "#10B981");
    public string OffColor     => Prop("offColor",    "#94A3B8");
    public string Orientation  => Prop("orientation", "horizontal");

    public string CurrentText  => IsOn ? OnText : OffText;
    public string CurrentColor => IsOn ? OnColor : OffColor;

    private string? ResolveTag()
    {
        var v = Prop("variable", "");
        if (!string.IsNullOrWhiteSpace(v)) return v;
        return Model.Binding?.TagId;
    }

    private static bool ParseBool(string s) =>
        !string.IsNullOrEmpty(s) && (string.Equals(s, "True", System.StringComparison.OrdinalIgnoreCase) || s == "1");

    partial void OnIsOnChanged(bool value)
    {
        OnPropertyChanged(nameof(CurrentText));
        OnPropertyChanged(nameof(CurrentColor));
    }

    [RelayCommand]
    private void Click()
    {
        if (!string.Equals(ModeProp, "bistable", System.StringComparison.OrdinalIgnoreCase)) return;
        var tag = ResolveTag();
        if (string.IsNullOrWhiteSpace(tag)) return;
        var newVal = !IsOn;
        _dataContext.ExecuteAction("write-bool", $"{tag}|{newVal}");
        IsOn = newVal;
    }

    [RelayCommand]
    private void PressDown()
    {
        if (!string.Equals(ModeProp, "momentary", System.StringComparison.OrdinalIgnoreCase)) return;
        var tag = ResolveTag();
        if (string.IsNullOrWhiteSpace(tag)) return;
        _dataContext.ExecuteAction("write-bool", $"{tag}|True");
        IsOn = true;
    }

    [RelayCommand]
    private void Release()
    {
        if (!string.Equals(ModeProp, "momentary", System.StringComparison.OrdinalIgnoreCase)) return;
        var tag = ResolveTag();
        if (string.IsNullOrWhiteSpace(tag)) return;
        _dataContext.ExecuteAction("write-bool", $"{tag}|False");
        IsOn = false;
    }
}
