using CommunityToolkit.Mvvm.ComponentModel;

namespace ApexHMI.Models;

public partial class TagItem : ObservableObject
{
    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string nodeId = string.Empty;

    [ObservableProperty]
    private string dataType = "String";

    [ObservableProperty]
    private string category = "General";

    [ObservableProperty]
    private string group = "Default";

    [ObservableProperty]
    private string direction = "Input";

    [ObservableProperty]
    private string currentValue = "";

    [ObservableProperty]
    private string description = string.Empty;

    [ObservableProperty]
    private bool isAlarm;

    [ObservableProperty]
    private bool isWritable;

    /// <summary>
    /// NodeId 模板：csv 中含 {OP}/{OP_TERM} 等占位符的原始值。
    /// NodeId 是 resolved 后实际 OPC UA 路径。
    /// 工位号变化时，TagNodeIdResolver 用 NodeIdTemplate 重新生成 NodeId。
    /// </summary>
    [ObservableProperty]
    private string nodeIdTemplate = string.Empty;
}
