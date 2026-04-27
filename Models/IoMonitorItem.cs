using CommunityToolkit.Mvvm.ComponentModel;

namespace ApexHMI.Models;

public partial class IoMonitorItem : ObservableObject
{
    [ObservableProperty] private int index;                    // 在 Monitor 数组中的索引 [0..15]
    [ObservableProperty] private string address = string.Empty; // 显示的地址文本（如 "DI[0]"）
    [ObservableProperty] private string comment = string.Empty; // IO 注释
    [ObservableProperty] private bool status;                   // 当前 IO 状态
    [ObservableProperty] private string statusTagName = string.Empty;  // OPC UA 变量名 (e.g. "OP80_DI_Mirror.Monitor[0].Status")
    [ObservableProperty] private string commentTagName = string.Empty; // OPC UA 变量名 (e.g. "OP80_DI_Mirror.Monitor[0].Comment")
    [ObservableProperty] private string direction = "DI";      // "DI" 或 "DO"
}
