#nullable enable
using System;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ApexHMI.Models.RuntimeUi;
using ApexHMI.Services.RuntimeUi;
using Serilog;

namespace ApexHMI.ViewModels.Runtime;

/// <summary>
/// 数字 I/O 域：Output 模式订阅 TagId 显示；Input 模式可输入并写回 PLC。
/// 读地址优先级：Properties["variable"] > Model.Binding.TagId。
/// </summary>
public partial class IoNumericWidgetViewModel : WidgetViewModelBase
{
    [ObservableProperty] private string _displayText = string.Empty;
    [ObservableProperty] private string _editText = string.Empty;

    private string _lastRawValue = string.Empty;

    public IoNumericWidgetViewModel(WidgetInstance model, IWidgetDataContext dataContext)
        : base(model, dataContext)
    {
        var tag = ResolveTag();
        if (!string.IsNullOrWhiteSpace(tag))
        {
            dataContext.RegisterValueCallback(tag, OnTagValueChanged);
        }
    }

    /// <summary>Input / Output / InputOutput。</summary>
    public string Mode            => Prop("mode",        "Output");
    public string Format          => Prop("format",      "0.##");
    public string DecimalsRaw     => Prop("decimals",    "2");
    public string Unit            => Prop("unit",        "");
    public string MinValueRaw     => Prop("minValue",    "");
    public string MaxValueRaw     => Prop("maxValue",    "");
    public string TextAlignment   => Prop("textAlign",   "Right");
    public string Background      => Prop("background",  "#FFFFFF");
    public string Foreground      => Prop("foreground",  "#0F172A");

    public bool IsInput  => Mode is "Input" or "InputOutput";
    public bool IsOutput => Mode is "Output" or "InputOutput";

    private string? ResolveTag()
    {
        var v = Prop("variable", "");
        if (!string.IsNullOrWhiteSpace(v)) return v;
        return Model.Binding?.TagId;
    }

    protected override void OnTagValueChanged(string rawValue)
    {
        _lastRawValue = rawValue;
        if (double.TryParse(rawValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var num))
        {
            string formatted;
            try
            {
                formatted = num.ToString(Format, CultureInfo.InvariantCulture);
            }
            catch (FormatException)
            {
                formatted = num.ToString(CultureInfo.InvariantCulture);
            }
            DisplayText = string.IsNullOrEmpty(Unit) ? formatted : $"{formatted} {Unit}";
            // Input 控件保留用户编辑中的内容；未编辑过时同步
            if (string.IsNullOrEmpty(EditText)) EditText = formatted;
        }
        else
        {
            DisplayText = rawValue;
        }
    }

    [RelayCommand]
    private void Commit()
    {
        if (!IsInput) return;
        var tag = ResolveTag();
        if (string.IsNullOrWhiteSpace(tag)) return;

        var text = EditText?.Trim() ?? string.Empty;
        if (!double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
        {
            Log.Warning("IoNumeric: 输入无法解析为数字 {Text}", text);
            return;
        }

        // 限值
        if (double.TryParse(MinValueRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var min) && v < min) v = min;
        if (double.TryParse(MaxValueRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var max) && v > max) v = max;

        // 写回类型按 Format / decimals 推断：小数位>0 用 float，否则 int
        var isInt = int.TryParse(DecimalsRaw, out var d) && d == 0;
        if (isInt)
        {
            _dataContext.ExecuteAction("write-int", $"{tag}|{(long)Math.Round(v)}");
        }
        else
        {
            _dataContext.ExecuteAction("write-float", $"{tag}|{v.ToString(CultureInfo.InvariantCulture)}");
        }
    }
}
