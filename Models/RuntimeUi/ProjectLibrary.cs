#nullable enable
using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ApexHMI.Models.RuntimeUi;

/// <summary>P6C: 库资产（包装一个 WidgetInstance 模板）。
/// <para>当用户把画布上的控件存入库时，深拷贝其 WidgetInstance；
/// 拖入画布时再次深拷贝并生成新 Id。</para></summary>
public partial class LibraryAsset : ObservableObject
{
    [ObservableProperty] private string _id = Guid.NewGuid().ToString("N");
    [ObservableProperty] private string _name = "未命名";
    [ObservableProperty] private string _category = "通用";

    /// <summary>核心数据：控件实例模板（不参与画布渲染，仅作模板）。</summary>
    public WidgetInstance Widget { get; set; } = new();
}

/// <summary>P6C: 工程级控件库。</summary>
public partial class ProjectLibrary : ObservableObject
{
    public ObservableCollection<LibraryAsset> Assets { get; set; } = new();
}
