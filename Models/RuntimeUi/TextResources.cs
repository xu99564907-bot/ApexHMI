#nullable enable
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ApexHMI.Models.RuntimeUi;

/// <summary>P6B: 单个多语言文本条目（Key → 语言代码 → 文本）。</summary>
public partial class TextEntry : ObservableObject
{
    [ObservableProperty] private string _key = "";

    /// <summary>语言代码（zh-CN/en-US/ja-JP 等）→ 显示文本。
    /// 用 Dictionary 是为了序列化更直观；运行时通常按 TextResources.SupportedLanguages 顺序填充。</summary>
    public Dictionary<string, string> Values { get; set; } = new();
}

/// <summary>P6B: 工程级文本资源集合。</summary>
public partial class TextResources : ObservableObject
{
    public ObservableCollection<TextEntry> Entries { get; set; } = new();

    [ObservableProperty] private string _defaultLanguage = "zh-CN";

    public ObservableCollection<string> SupportedLanguages { get; set; } = new() { "zh-CN", "en-US" };

    /// <summary>注入示例条目（仅当为空时）。</summary>
    public void EnsureDefaults()
    {
        if (SupportedLanguages.Count == 0)
        {
            SupportedLanguages.Add("zh-CN");
            SupportedLanguages.Add("en-US");
        }
        // 不强行注入条目；用户自行添加
    }
}
