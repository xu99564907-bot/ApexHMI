#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ApexHMI.Models.RuntimeUi;

/// <summary>P8A 配方字段数据类型。</summary>
public enum RecipeFieldType
{
    String,
    Number,
    Integer,
    Boolean
}

/// <summary>P8A 配方字段定义：一个字段对应 PLC 上的一个变量地址。</summary>
public partial class RecipeField : ObservableObject
{
    [ObservableProperty] private string _key = "";
    [ObservableProperty] private string _displayName = "";
    [ObservableProperty] private RecipeFieldType _type = RecipeFieldType.Number;

    /// <summary>写入 / 读出 PLC 时使用的 Tag 地址。</summary>
    [ObservableProperty] private string _tagAddress = "";
    [ObservableProperty] private string _defaultValue = "";
    [ObservableProperty] private string _unit = "";
}

/// <summary>P8A 配方数据集：一份具体参数。</summary>
public partial class RecipeDataset : ObservableObject
{
    [ObservableProperty] private string _id = Guid.NewGuid().ToString("N");
    [ObservableProperty] private string _name = "数据集";
    [ObservableProperty] private string _description = "";
    [ObservableProperty] private DateTime _modifiedAt = DateTime.Now;

    /// <summary>字段 Key → 值（用字符串存，写 PLC 时按 RecipeField.Type 转换）。</summary>
    public Dictionary<string, string> Values { get; set; } = new();
}

/// <summary>P8A 配方定义：一组字段 + 多份数据集。</summary>
public partial class Recipe : ObservableObject
{
    [ObservableProperty] private string _id = Guid.NewGuid().ToString("N");
    [ObservableProperty] private string _name = "配方";
    [ObservableProperty] private string _category = "通用";

    public ObservableCollection<RecipeField> Fields { get; set; } = new();
    public ObservableCollection<RecipeDataset> Datasets { get; set; } = new();

    /// <summary>M5.1: 是否走 WinCC Job Mailbox 4-word 握手协议。默认 false（兼容旧工程的 fire-and-forget 直接写）。</summary>
    [ObservableProperty] private bool _useJobMailbox;

    /// <summary>M5.1: Job Mailbox 4-word 握手协议配置。</summary>
    public RecipeJobMailbox Mailbox { get; set; } = new();
}

/// <summary>P8A 工程级配方库（挂在 ProjectDocument 上，随工程一起 JSON 落盘）。</summary>
public partial class RecipeLibrary : ObservableObject
{
    public ObservableCollection<Recipe> Recipes { get; set; } = new();
}
