#nullable enable
using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ApexHMI.Models.RuntimeUi;

/// <summary>P6E: 文本列表条目（INT 值 → 文字）。</summary>
public partial class TextListItem : ObservableObject
{
    [ObservableProperty] private string _value = "0";
    [ObservableProperty] private string _text = "";
}

/// <summary>P6E: 工程级文本列表（被 io-symbolic 引用，例如运行状态 0=停止/1=运行/2=报警）。</summary>
public partial class TextList : ObservableObject
{
    [ObservableProperty] private string _id = Guid.NewGuid().ToString("N");
    [ObservableProperty] private string _name = "未命名列表";
    public ObservableCollection<TextListItem> Items { get; set; } = new();
}

/// <summary>P6E: 图形列表条目（INT 值 → 图片路径）。</summary>
public partial class GraphicListItem : ObservableObject
{
    [ObservableProperty] private string _value = "0";
    [ObservableProperty] private string _image = "";
}

/// <summary>P6E: 工程级图形列表（被 io-graphic 引用）。</summary>
public partial class GraphicList : ObservableObject
{
    [ObservableProperty] private string _id = Guid.NewGuid().ToString("N");
    [ObservableProperty] private string _name = "未命名列表";
    public ObservableCollection<GraphicListItem> Items { get; set; } = new();
}

/// <summary>P6E: 工程级列表资源集合容器。</summary>
public partial class ListResources : ObservableObject
{
    public ObservableCollection<TextList> TextLists { get; set; } = new();
    public ObservableCollection<GraphicList> GraphicLists { get; set; } = new();
}
