using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ApexHMI.Models.RuntimeUi;

/// <summary>
/// 属性网格中的单个属性行，用于双向绑定 WidgetInstance.Properties 字典条目。
/// </summary>
public partial class WidgetPropertyItem : ObservableObject
{
    /// <summary>属性键 → 中文显示名映射，让属性面板更友好。</summary>
    private static readonly Dictionary<string, string> KeyDisplayNames = new(System.StringComparer.OrdinalIgnoreCase)
    {
        ["text"]          = "文本",
        ["label"]         = "标签",
        ["fontSize"]      = "字号",
        ["fontWeight"]    = "字重",
        ["foreground"]    = "前景色",
        ["background"]    = "背景色",
        ["textAlign"]     = "对齐方式",
        ["trueColor"]     = "True 颜色",
        ["falseColor"]    = "False 颜色",
        ["trueText"]      = "True 文本",
        ["falseText"]     = "False 文本",
        ["unit"]          = "单位",
        ["format"]        = "数值格式",
        ["runningColor"]  = "运行颜色",
        ["stoppedColor"]  = "停止颜色",
        ["homeColor"]     = "原位颜色",
        ["workColor"]     = "动作颜色",
        ["upColor"]       = "升起颜色",
        ["downColor"]     = "落下颜色",
        ["busyColor"]     = "忙碌颜色",
        ["idleColor"]     = "空闲颜色",
        ["activeColor"]   = "激活颜色",
        ["inactiveColor"] = "未激活颜色",
    };

    public WidgetPropertyItem(string key, string value)
    {
        _key = key;
        _value = value;
        _displayName = KeyDisplayNames.TryGetValue(key, out var name) ? name : key;
    }

    [ObservableProperty]
    private string _key;

    [ObservableProperty]
    private string _value;

    /// <summary>显示名（优先用中文，未注册的回落到原 Key）。</summary>
    [ObservableProperty]
    private string _displayName;

    /// <summary>是否颜色类属性（值以 # 开头），用于决定是否显示色板预览。</summary>
    public bool IsColorProperty => !string.IsNullOrEmpty(Value) && Value.StartsWith("#", System.StringComparison.Ordinal);

    partial void OnValueChanged(string value)
    {
        OnPropertyChanged(nameof(IsColorProperty));
    }
}
