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

        // P6B: 语言切换 → 通知所有属性变化以刷新文本绑定
        DesignerContext.LanguageChanged += OnLanguageOrResourcesChanged;
        DesignerContext.ResourcesChanged += OnResourcesChanged;
        // 注：不在这里监听 Model.PropertyChanged 触发 OnPropertyChanged(string.Empty)。
    }

    private void OnLanguageOrResourcesChanged(string _) => OnPropertyChanged(string.Empty);
    private void OnResourcesChanged() => OnPropertyChanged(string.Empty);

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

    /// <summary>便捷：从 Properties 取值，无则返回 fallback。
    /// <para>P3.2 i18n: 如果值是 ${KEY} 形式，自动解析为内置 .resx 本地化资源。</para>
    /// <para>P6A: 如果值是 <c>{style:colors/xxx}</c> / <c>{style:fonts/xxx}</c>，解析工程级样式。</para>
    /// <para>P6B: 如果值是 <c>{text:keyName}</c>，按当前语言解析工程级多语言文本。</para>
    /// </summary>
    protected string Prop(string key, string fallback = "")
    {
        var raw = Model.Properties.TryGetValue(key, out var v) ? v : fallback;
        raw = ResolveLocalized(raw);
        // P7B: 若处于 Faceplate 实例的 InnerScreen 上下文，先解析 {prop:keyName} 引用为实例属性值
        raw = FaceplateResolver.Resolve(raw, _dataContext.CurrentFaceplateProperties);
        raw = StyleResolver.Resolve(raw, DesignerContext.Document?.Styles);
        raw = TextResolver.Resolve(raw, DesignerContext.Document?.Texts, DesignerContext.CurrentLanguage);
        return raw;
    }

    /// <summary>取原始 Property 值（不做任何解析），用于属性编辑器需要看到 <c>{style:...}</c> 等原始引用语法的场景。</summary>
    protected string PropRaw(string key, string fallback = "")
        => Model.Properties.TryGetValue(key, out var v) ? v : fallback;

    /// <summary>解析 ${KEY} 引用为本地化文本（不匹配时原样返回）。</summary>
    protected static string ResolveLocalized(string raw)
    {
        if (string.IsNullOrEmpty(raw) || !raw.StartsWith("${") || !raw.EndsWith("}"))
            return raw;
        var key = raw.Substring(2, raw.Length - 3);
        var loc = ApexHMI.Converters.LocExtension.LocalizationService;
        return loc?.GetString(key) ?? raw;
    }
}
