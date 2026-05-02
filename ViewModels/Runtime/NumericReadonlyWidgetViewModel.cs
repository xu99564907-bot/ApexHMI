using CommunityToolkit.Mvvm.ComponentModel;
using ApexHMI.Models.RuntimeUi;
using ApexHMI.Services.RuntimeUi;

namespace ApexHMI.ViewModels.Runtime;

/// <summary>只读数值显示：支持格式化、单位。</summary>
public partial class NumericReadonlyWidgetViewModel : WidgetViewModelBase
{
    [ObservableProperty]
    private string _displayValue = "--";

    public NumericReadonlyWidgetViewModel(WidgetInstance model, IWidgetDataContext dataContext)
        : base(model, dataContext) { }

    public string Label  => Prop("label",  string.Empty);
    public string Unit   => Prop("unit",   string.Empty);
    public string Format => Prop("format", "F1");

    protected override void OnTagValueChanged(string rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            DisplayValue = "--";
            return;
        }

        if (double.TryParse(rawValue, out var num))
        {
            try { DisplayValue = num.ToString(Format) + (string.IsNullOrEmpty(Unit) ? "" : $" {Unit}"); }
            catch { DisplayValue = rawValue; }
        }
        else
        {
            DisplayValue = rawValue;
        }
    }
}
