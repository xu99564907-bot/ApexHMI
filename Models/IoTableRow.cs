using CommunityToolkit.Mvvm.ComponentModel;

namespace ApexHMI.Models;

public partial class IoTableRow : ObservableObject
{
    [ObservableProperty]
    private string inputModule = string.Empty;

    [ObservableProperty]
    private string inputAddress = string.Empty;

    [ObservableProperty]
    private string inputStation = string.Empty;

    [ObservableProperty]
    private string inputComment = string.Empty;

    [ObservableProperty]
    private string inputRemark = string.Empty;

    [ObservableProperty]
    private string outputModule = string.Empty;

    [ObservableProperty]
    private string outputAddress = string.Empty;

    [ObservableProperty]
    private string outputStation = string.Empty;

    [ObservableProperty]
    private string outputComment = string.Empty;

    [ObservableProperty]
    private string outputRemark = string.Empty;

    // D1: 导入校验失败标记 + 错误描述（重复地址 / 类型不匹配）
    [ObservableProperty]
    private bool hasError;

    [ObservableProperty]
    private string validationError = string.Empty;
}
