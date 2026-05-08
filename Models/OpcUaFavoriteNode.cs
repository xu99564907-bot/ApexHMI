using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ApexHMI.Models;

/// <summary>M16: OPC UA 节点收藏夹条目（持久化到 config/opc-favorites.json）。</summary>
public partial class OpcUaFavoriteNode : ObservableObject
{
    [ObservableProperty]
    private string displayName = string.Empty;

    [ObservableProperty]
    private string nodeId = string.Empty;

    [ObservableProperty]
    private string note = string.Empty;

    [ObservableProperty]
    private DateTime addedAt = DateTime.Now;
}
