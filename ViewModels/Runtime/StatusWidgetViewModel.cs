using CommunityToolkit.Mvvm.ComponentModel;
using ApexHMI.Models.RuntimeUi;
using ApexHMI.Services.RuntimeUi;

namespace ApexHMI.ViewModels.Runtime;

/// <summary>
/// 通用工业状态控件 ViewModel。
/// 支持根据绑定的 Tag 值切换颜色和文字，适用于 Motor/Cylinder/Axis/Robot/Stopper/AlarmBanner。
///
/// Properties 约定：
///   "label"         → 显示标签文本
///   "trueColor"     → 绑定值为真时的颜色（默认 #22C55E）
///   "falseColor"    → 绑定值为假时的颜色（默认 #94A3B8）
///   "trueText"      → 绑定值为真时显示的状态文字
///   "falseText"     → 绑定值为假时显示的状态文字
///   "format"        → 数值显示格式（可选，如 "F0"）
/// </summary>
public partial class StatusWidgetViewModel : WidgetViewModelBase
{
    [ObservableProperty]
    private string _currentColor = "#94A3B8";

    [ObservableProperty]
    private string _statusText = "--";

    public StatusWidgetViewModel(WidgetInstance model, IWidgetDataContext dataContext)
        : base(model, dataContext)
    {
        UpdateDisplay("false");
    }

    public string Label => Prop("label", Model.TypeId);
    public string TrueColor => Prop("trueColor", "#22C55E");
    public string FalseColor => Prop("falseColor", "#94A3B8");
    public string TrueText => Prop("trueText", "ON");
    public string FalseText => Prop("falseText", "OFF");

    protected override void OnTagValueChanged(string rawValue)
    {
        UpdateDisplay(rawValue);
    }

    private void UpdateDisplay(string rawValue)
    {
        var isTrue = rawValue is "1" or "True" or "true" or "TRUE";

        if (Model.Binding?.DataType is "Int" or "Float")
        {
            // 数值类型：尝试格式化显示
            var format = Prop("format", string.Empty);
            if (double.TryParse(rawValue,
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var num))
            {
                StatusText = string.IsNullOrEmpty(format) ? num.ToString() : num.ToString(format);
            }
            else
            {
                StatusText = rawValue;
            }

            CurrentColor = "#3B82F6";
        }
        else
        {
            StatusText = isTrue ? TrueText : FalseText;
            CurrentColor = isTrue ? TrueColor : FalseColor;
        }
    }
}
