#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using ApexHMI.Models.RuntimeUi;

namespace ApexHMI.ViewModels.Modules;

/// <summary>
/// P7.5: 一个属性编辑行的 ViewModel。把 <see cref="PropertyDescriptor"/> + 当前字符串值
/// 暴露给 DataTemplate；Value 变化时回调外部 setter 写入 WidgetInstance.Properties。
/// </summary>
public partial class PropertyEditorVM : ObservableObject
{
    private readonly Action<string, string?>? _onChanged;
    private bool _suppressNotify;

    public PropertyEditorVM(PropertyDescriptor descriptor, string value, Action<string, string?>? onChanged = null)
    {
        Descriptor = descriptor;
        _value = value;
        _onChanged = onChanged;
        EnumOptionItems = BuildEnumOptions(descriptor);
    }

    public PropertyDescriptor Descriptor { get; }

    /// <summary>属性 key（与 WidgetInstance.Properties 字典 key 一致）。</summary>
    public string Key => Descriptor.Key;

    /// <summary>显示名（中文友好）。</summary>
    public string DisplayName => Descriptor.DisplayName;

    /// <summary>Tooltip 描述。</summary>
    public string Description => string.IsNullOrEmpty(Descriptor.Description) ? Descriptor.Key : Descriptor.Description;

    /// <summary>编辑器类型枚举（用于 TemplateSelector 选模板）。</summary>
    public PropertyEditorType EditorType => Descriptor.EditorType;

    /// <summary>当前值（字符串形式；具体类型由编辑器解析）。</summary>
    [ObservableProperty]
    private string _value = string.Empty;

    partial void OnValueChanged(string value)
    {
        if (_suppressNotify) return;
        _onChanged?.Invoke(Key, value);
        OnPropertyChanged(nameof(BoolValue));
    }

    /// <summary>外部更新值时使用，不再触发回调，避免环。</summary>
    public void SetValueSilent(string value)
    {
        _suppressNotify = true;
        try { Value = value; }
        finally { _suppressNotify = false; }
        OnPropertyChanged(nameof(BoolValue));
    }

    /// <summary>布尔编辑器用的双向桥接。</summary>
    public bool BoolValue
    {
        get => string.Equals(Value, "true", StringComparison.OrdinalIgnoreCase) || Value == "1";
        set
        {
            var newVal = value ? "true" : "false";
            if (!string.Equals(Value, newVal, StringComparison.OrdinalIgnoreCase))
                Value = newVal;
        }
    }

    /// <summary>枚举选项（每项 value + display）。</summary>
    public IReadOnlyList<EnumOption> EnumOptionItems { get; }

    private static IReadOnlyList<EnumOption> BuildEnumOptions(PropertyDescriptor desc)
    {
        if (desc.EnumOptions is null) return Array.Empty<EnumOption>();
        var list = new List<EnumOption>(desc.EnumOptions.Count);
        foreach (var raw in desc.EnumOptions)
        {
            var sep = raw.IndexOf('|');
            if (sep < 0) list.Add(new EnumOption(raw, raw));
            else list.Add(new EnumOption(raw.Substring(0, sep), raw.Substring(sep + 1)));
        }
        return list;
    }
}

/// <summary>P7.5: 单条枚举选项（value/label 分离，绑定 ComboBox 用）。</summary>
public sealed class EnumOption
{
    public EnumOption(string value, string display) { Value = value; Display = display; }
    public string Value { get; }
    public string Display { get; }
}

/// <summary>
/// P7.5: 属性分组（按 Category 折叠，Expander 一组）。
/// </summary>
public sealed class PropertyCategoryGroup
{
    public PropertyCategoryGroup(string category, IEnumerable<PropertyEditorVM> items)
    {
        Category = category;
        Items = new ObservableCollection<PropertyEditorVM>(items);
    }
    public string Category { get; }
    public ObservableCollection<PropertyEditorVM> Items { get; }
}
