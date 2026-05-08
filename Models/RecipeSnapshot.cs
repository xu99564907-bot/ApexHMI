using System;
using System.Collections.ObjectModel;

namespace ApexHMI.Models;

/// <summary>R5: 配方历史版本快照（每次"用当前参数覆盖配方"时入栈）。</summary>
public sealed class RecipeSnapshot
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string User { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ObservableCollection<ParameterItem> Parameters { get; set; } = new();
}
