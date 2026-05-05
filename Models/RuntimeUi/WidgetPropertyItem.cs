using CommunityToolkit.Mvvm.ComponentModel;

namespace ApexHMI.Models.RuntimeUi;

/// <summary>
/// 属性网格中的单个属性行，用于双向绑定 WidgetInstance.Properties 字典条目。
/// </summary>
public partial class WidgetPropertyItem : ObservableObject
{
    public WidgetPropertyItem(string key, string value)
    {
        _key = key;
        _value = value;
    }

    [ObservableProperty]
    private string _key;

    [ObservableProperty]
    private string _value;
}
