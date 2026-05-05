using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using ApexHMI.Models.RuntimeUi;
using ApexHMI.Services.RuntimeUi;
using ApexHMI.ViewModels;

namespace ApexHMI.ViewModels.Runtime;

/// <summary>
/// 通用 OPC UA Tag 值显示 widget。
/// Properties:
///   "tagName"  目标 Tag 名（必填）
///   "label"    显示标签
///   "format"   数值格式（如 F2 / N0）；为空时直接显示原始字符串
///   "unit"     单位
/// 实时从 Shell.Tags 中查找对应 Tag,订阅其 PropertyChanged 自动刷新显示值。
/// </summary>
public partial class OpcTagValueWidgetViewModel : WidgetViewModelBase
{
    private readonly MainViewModel? _shell;
    private readonly Models.TagItem? _tagRef;

    [ObservableProperty]
    private string _displayValue = "--";

    public OpcTagValueWidgetViewModel(WidgetInstance model, IWidgetDataContext dataContext)
        : base(model, dataContext)
    {
        _shell = dataContext.Shell as MainViewModel;
        var tagName = Prop("tagName", string.Empty);
        if (_shell is not null && !string.IsNullOrWhiteSpace(tagName))
        {
            _tagRef = _shell.Tags.FirstOrDefault(t =>
                string.Equals(t.Name, tagName, System.StringComparison.OrdinalIgnoreCase));
            if (_tagRef is not null)
            {
                _tagRef.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(Models.TagItem.CurrentValue))
                        UpdateDisplay();
                };
                UpdateDisplay();
            }
        }
    }

    public string Label => Prop("label", Prop("tagName", string.Empty));
    public string Unit  => Prop("unit", string.Empty);

    private void UpdateDisplay()
    {
        if (_tagRef is null)
        {
            DisplayValue = $"未找到 Tag: {Prop("tagName", string.Empty)}";
            return;
        }
        var format = Prop("format", string.Empty);
        if (!string.IsNullOrWhiteSpace(format) &&
            double.TryParse(_tagRef.CurrentValue,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out var num))
        {
            DisplayValue = num.ToString(format);
        }
        else
        {
            DisplayValue = _tagRef.CurrentValue ?? "--";
        }
    }
}
