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
    [ObservableProperty] private bool? _isCheckedState;
    private bool _initializingFromTag;

    public CheckBoxWidgetViewModel(WidgetInstance model, IWidgetDataContext dataContext)
        : base(model, dataContext)
    {
        var tag = ResolveTag();
        if (!string.IsNullOrWhiteSpace(tag))
            dataContext.RegisterValueCallback(tag!, v =>
            {
                _initializingFromTag = true;
                try { IsCheckedState = ParseTriState(v, ThreeState); }
                finally { _initializingFromTag = false; }
            });
    }

    /// <summary>M4.4: CheckBox 总参与 Tab 焦点链（可点击勾选）。</summary>
    public bool IsTabStop => true;

    public string Text             => Prop("text",             "选项");
    public string CheckedColor     => Prop("checkedColor",     "#10B981");
    public string UncheckedColor   => Prop("uncheckedColor",   "#94A3B8");
    public string Foreground       => Prop("foreground",       "#0F172A");

    // B3.2: WinCC CheckBox 扩展
    public bool ThreeState =>
        string.Equals(Prop("threeState", "false"), "true", System.StringComparison.OrdinalIgnoreCase);
    public string CheckedText      => Prop("checkedText", "");
    public string UncheckedText    => Prop("uncheckedText", "");
    public string IndeterminateText => Prop("indeterminateText", "未定");
    public string IndeterminateColor => Prop("indeterminateColor", "#FBBF24");

    /// <summary>B3.2: 按当前状态选择文本，留空时回退到 Text。</summary>
    public string DisplayText => IsCheckedState switch
    {
        true => string.IsNullOrEmpty(CheckedText) ? Text : CheckedText,
        false => string.IsNullOrEmpty(UncheckedText) ? Text : UncheckedText,
        null => IndeterminateText,
    };

    /// <summary>B3.2: View 用 IsThreeState 绑定。</summary>
    public bool IsThreeStateEnabled => ThreeState;

    private string? ResolveTag()
    {
        var v = Prop("variable", "");
        if (!string.IsNullOrWhiteSpace(v)) return v;
        return Model.Binding?.TagId;
    }

    private static bool? ParseTriState(string s, bool threeState)
    {
        if (string.IsNullOrEmpty(s)) return threeState ? (bool?)null : false;
        if (string.Equals(s, "True", System.StringComparison.OrdinalIgnoreCase) || s == "1") return true;
        if (string.Equals(s, "False", System.StringComparison.OrdinalIgnoreCase) || s == "0") return false;
        // -1 / null / "indeterminate" 等
        if (threeState && (s == "-1" || string.Equals(s, "null", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(s, "indeterminate", System.StringComparison.OrdinalIgnoreCase))) return null;
        return false;
    }

    partial void OnIsCheckedStateChanged(bool? value)
    {
        OnPropertyChanged(nameof(DisplayText));
        if (_initializingFromTag) return;
        var tag = ResolveTag();
        if (string.IsNullOrWhiteSpace(tag)) return;
        // 三态写回：true→1，false→0，null→-1（WinCC 兼容惯例）
        var w = value switch
        {
            true => "True",
            false => "False",
            null => "-1",
        };
        _dataContext.ExecuteAction("write-bool", $"{tag}|{w}");
    }
}
