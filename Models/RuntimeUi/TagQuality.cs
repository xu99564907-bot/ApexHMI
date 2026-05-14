using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ApexHMI.Models.RuntimeUi;

/// <summary>
/// M3.1: Tag 质量码 — 映射 OPC UA DataValue.StatusCode（Good/Uncertain/Bad）。
/// WinCC 真实行为：UI 按 quality 区分显示（Good 正常；Bad 显示 ####；Uncertain 加黄三角）。
/// </summary>
public enum TagQuality
{
    Good = 0,
    Uncertain = 1,
    Bad = 2,
}

/// <summary>M3.1: 带质量+时间戳的 Tag 值包装。运行时 ValueChanged 回调可消费此对象。</summary>
public partial class TagValue : ObservableObject
{
    [ObservableProperty]
    private string _value = string.Empty;

    [ObservableProperty]
    private TagQuality _quality = TagQuality.Good;

    [ObservableProperty]
    private DateTime _timestamp = DateTime.MinValue;

    public TagValue() { }

    public TagValue(string value, TagQuality quality, DateTime timestamp)
    {
        _value = value;
        _quality = quality;
        _timestamp = timestamp;
    }
}
