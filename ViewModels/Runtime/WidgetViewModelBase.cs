using CommunityToolkit.Mvvm.ComponentModel;
using ApexHMI.Models.RuntimeUi;
using ApexHMI.Services.RuntimeUi;

namespace ApexHMI.ViewModels.Runtime;

/// <summary>所有运行时 Widget ViewModel 的基类。</summary>
public abstract partial class WidgetViewModelBase : ObservableObject
{
    protected readonly IWidgetDataContext _dataContext;

    protected WidgetViewModelBase(WidgetInstance model, IWidgetDataContext dataContext)
    {
        Model = model;
        _dataContext = dataContext;

        if (model.Binding is { TagId.Length: > 0 } binding)
        {
            dataContext.RegisterValueCallback(binding.TagId, OnTagValueChanged);
        }

        // P2.5 状态动画：每个动画的 TagId 注册回调，触发时重新计算属性
        foreach (var anim in model.Animations)
        {
            if (string.IsNullOrWhiteSpace(anim.TagId)) continue;
            var captured = anim;
            dataContext.RegisterValueCallback(captured.TagId, val => ApplyAnimation(captured, val));
        }

        // 注：不在这里监听 Model.PropertyChanged 触发 OnPropertyChanged(string.Empty)。
    }

    /// <summary>计算并应用单个动画规则。</summary>
    private void ApplyAnimation(WidgetAnimation anim, string rawValue)
    {
        bool match = anim.Op?.ToLowerInvariant() switch
        {
            "true"  => rawValue is "1" or "True" or "true" or "TRUE",
            "false" => !(rawValue is "1" or "True" or "true" or "TRUE"),
            "eq"    => string.Equals(rawValue, anim.CompareTo, System.StringComparison.OrdinalIgnoreCase),
            "ne"    => !string.Equals(rawValue, anim.CompareTo, System.StringComparison.OrdinalIgnoreCase),
            "gt"    => CompareNum(rawValue, anim.CompareTo) > 0,
            "lt"    => CompareNum(rawValue, anim.CompareTo) < 0,
            "gte"   => CompareNum(rawValue, anim.CompareTo) >= 0,
            "lte"   => CompareNum(rawValue, anim.CompareTo) <= 0,
            _       => false
        };

        if (match && !string.IsNullOrEmpty(anim.TargetProperty))
        {
            // 写入 Model.Properties 触发 NotifyPropertiesChanged → DesignerEditor 中 view 自动重建
            Model.Properties[anim.TargetProperty] = anim.TargetValue;
            Model.NotifyPropertiesChanged();
        }
    }

    private static int CompareNum(string a, string b)
    {
        if (double.TryParse(a, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var x) &&
            double.TryParse(b, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var y))
            return x.CompareTo(y);
        return string.Compare(a, b, System.StringComparison.OrdinalIgnoreCase);
    }

    public WidgetInstance Model { get; }

    protected virtual void OnTagValueChanged(string rawValue) { }

    /// <summary>便捷：从 Properties 取值，无则返回 fallback。</summary>
    protected string Prop(string key, string fallback = "") =>
        Model.Properties.TryGetValue(key, out var v) ? v : fallback;
}
