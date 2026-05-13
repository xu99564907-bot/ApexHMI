#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ApexHMI.Models.RuntimeUi;
using ApexHMI.Services.RuntimeUi;

namespace ApexHMI.ViewModels.Runtime;

/// <summary>
/// P8E 状态强制视图（调试用）：列出 widget 属性 <c>tags</c> 中配置的 Tag 地址，
/// 实时显示当前值并允许强制写入新值（write-int 默认）。
/// </summary>
public partial class StatusForceWidgetViewModel : WidgetViewModelBase
{
    public StatusForceWidgetViewModel(WidgetInstance model, IWidgetDataContext dataContext)
        : base(model, dataContext)
    {
        Rebuild();
    }

    public bool ReadOnly => string.Equals(Prop("readonly", "false"), "true", StringComparison.OrdinalIgnoreCase);
    public string Background => Prop("background", "#FFFFFF");
    public string Foreground => Prop("foreground", "#0F172A");

    public ObservableCollection<TagForceItem> Items { get; } = new();

    private void Rebuild()
    {
        Items.Clear();
        var raw = Prop("tags", "");
        if (string.IsNullOrWhiteSpace(raw)) return;
        var parts = raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                       .Select(s => s.Trim())
                       .Where(s => s.Length > 0)
                       .Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var tag in parts)
        {
            var item = new TagForceItem(tag, this);
            Items.Add(item);
            var captured = item;
            _dataContext.RegisterValueCallback(tag, v =>
            {
                captured.CurrentValue = v;
                if (string.IsNullOrEmpty(captured.ForceValue)) captured.ForceValue = v;
            });
        }
    }

    [RelayCommand]
    private void ApplyForce(TagForceItem? item)
    {
        if (ReadOnly || item is null || string.IsNullOrWhiteSpace(item.TagAddress)) return;
        // 默认走 write-int；如已知类型可在后续扩展
        _dataContext.ExecuteAction("write-int", $"{item.TagAddress}|{item.ForceValue}");
    }
}

/// <summary>P8E 强制行：Tag 地址、当前值、待写值。</summary>
public partial class TagForceItem : ObservableObject
{
    private readonly StatusForceWidgetViewModel _owner;

    public TagForceItem(string tag, StatusForceWidgetViewModel owner)
    {
        TagAddress = tag;
        _owner = owner;
    }

    public string TagAddress { get; }

    [ObservableProperty] private string _currentValue = "—";
    [ObservableProperty] private string _forceValue = "";

    public StatusForceWidgetViewModel Owner => _owner;
}
