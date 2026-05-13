#nullable enable
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using ApexHMI.Models.RuntimeUi;
using ApexHMI.Services.RuntimeUi;

namespace ApexHMI.ViewModels.Runtime;

/// <summary>
/// 单个选项：value（写到 PLC）+ text（显示）。
/// </summary>
public class WidgetOptionEntry
{
    public int Value { get; set; }
    public string Text { get; set; } = string.Empty;
    public override string ToString() => Text;
}

/// <summary>
/// 组合框 / 列表框 / 单选 共用 ViewModel：解析 items 字符串生成选项列表，
/// 当 PLC Tag 值变化 → 反向选中匹配项；用户选择 → 写回 INT。
/// </summary>
public partial class OptionItemsWidgetViewModel : WidgetViewModelBase
{
    public ObservableCollection<WidgetOptionEntry> Items { get; } = new();

    [ObservableProperty] private WidgetOptionEntry? _selectedItem;
    private bool _initializingFromTag;

    public OptionItemsWidgetViewModel(WidgetInstance model, IWidgetDataContext dataContext)
        : base(model, dataContext)
    {
        ParseItems();
        var tag = ResolveTag();
        if (!string.IsNullOrWhiteSpace(tag))
            dataContext.RegisterValueCallback(tag!, OnTagValueChanged);
    }

    public string OrientationProp => Prop("orientation", "vertical");
    public string ItemsRaw        => Prop("items", "");

    private string? ResolveTag()
    {
        var v = Prop("variable", "");
        if (!string.IsNullOrWhiteSpace(v)) return v;
        return Model.Binding?.TagId;
    }

    /// <summary>
    /// items 解析：支持两种格式
    /// 1) "0=停止;1=运行" 分号分隔
    /// 2) JSON-ish "[{value:0,text:'停止'},...]" 简化为分号格式优先
    /// </summary>
    private void ParseItems()
    {
        Items.Clear();
        var raw = ItemsRaw?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(raw)) return;
        foreach (var part in raw.Split(';', ','))
        {
            var s = part.Trim();
            if (string.IsNullOrEmpty(s)) continue;
            var eq = s.IndexOf('=');
            int value;
            string text;
            if (eq > 0 && int.TryParse(s.Substring(0, eq), out value))
            {
                text = s.Substring(eq + 1);
            }
            else
            {
                value = Items.Count;
                text = s;
            }
            Items.Add(new WidgetOptionEntry { Value = value, Text = text });
        }
    }

    protected override void OnTagValueChanged(string rawValue)
    {
        if (!int.TryParse(rawValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var v)) return;
        var match = Items.FirstOrDefault(i => i.Value == v);
        if (match is null) return;
        _initializingFromTag = true;
        try { SelectedItem = match; }
        finally { _initializingFromTag = false; }
    }

    partial void OnSelectedItemChanged(WidgetOptionEntry? value)
    {
        if (_initializingFromTag || value is null) return;
        var tag = ResolveTag();
        if (string.IsNullOrWhiteSpace(tag)) return;
        _dataContext.ExecuteAction("write-int", $"{tag}|{value.Value}");
    }
}
