using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ApexHMI.Models;

public partial class OperationAuditRecord : ObservableObject
{
    [ObservableProperty]
    private DateTime time = DateTime.Now;

    [ObservableProperty]
    private string user = string.Empty;

    [ObservableProperty]
    private string action = string.Empty;

    [ObservableProperty]
    private string target = string.Empty;

    [ObservableProperty]
    private string result = string.Empty;

    [ObservableProperty]
    private string detail = string.Empty;

    // AU6: 动作分类（登录类 / 设备操作 / 参数修改 / 报警处理 / 系统事件 / 其他）
    // 由 AddAudit() 在写入时根据 Action 关键字自动归类
    [ObservableProperty]
    private string category = "其他";
}
