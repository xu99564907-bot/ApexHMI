using CommunityToolkit.Mvvm.ComponentModel;
using ApexHMI.Models.RuntimeUi;
using ApexHMI.Services.RuntimeUi;

namespace ApexHMI.ViewModels.Runtime;

/// <summary>布尔指示灯：True/False 显示不同颜色。</summary>
public partial class BoolLampWidgetViewModel : WidgetViewModelBase
{
    [ObservableProperty]
    private bool _isOn;

    public BoolLampWidgetViewModel(WidgetInstance model, IWidgetDataContext dataContext)
        : base(model, dataContext) { }

    public string Label => Prop("label", string.Empty);
    public string TrueColor  => Prop("trueColor",  "#22C55E");
    public string FalseColor => Prop("falseColor", "#94A3B8");
    public string CurrentColor => IsOn ? TrueColor : FalseColor;

    protected override void OnTagValueChanged(string rawValue)
    {
        IsOn = rawValue is "1" or "True" or "true" or "TRUE";
        OnPropertyChanged(nameof(CurrentColor));
    }
}
