using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ApexHMI.Models;

public partial class OpcUaBrowseNode : ObservableObject
{
    public ObservableCollection<OpcUaBrowseNode> Children { get; } = new();

    [ObservableProperty] private string displayName = string.Empty;
    [ObservableProperty] private string nodeId = string.Empty;
    [ObservableProperty] private string nodeClass = string.Empty;
    [ObservableProperty] private string dataType = "--";
    [ObservableProperty] private string value = "--";
    [ObservableProperty] private bool hasChildren;
    [ObservableProperty] private bool isLoaded;
    [ObservableProperty] private bool isPlaceholder;

    // M17 节点搜索高亮标记（OpcUaBrowserSearchText 命中时为 true，TreeView DataTrigger 高亮）
    [ObservableProperty] private bool isSearchHit;

    public static OpcUaBrowseNode CreatePlaceholder()
    {
        return new OpcUaBrowseNode
        {
            DisplayName = "Loading",
            IsPlaceholder = true
        };
    }
}
