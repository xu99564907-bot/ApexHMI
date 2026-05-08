using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ApexHMI.Models;

public partial class RecipeItem : ObservableObject
{
    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string productCode = string.Empty;

    [ObservableProperty]
    private string version = "V1.0";

    [ObservableProperty]
    private string description = string.Empty;

    [ObservableProperty]
    private bool isActive;

    [ObservableProperty]
    private DateTime updatedAt = DateTime.Now;

    [ObservableProperty]
    private string updatedBy = "System";

    [ObservableProperty]
    private ObservableCollection<ParameterItem> parameters = new();

    // R5: 历史版本（每次"用当前参数覆盖"自动入栈，可回滚）
    public ObservableCollection<RecipeSnapshot> History { get; set; } = new();

    // R7: 上次使用时间（ApplyRecipe 时刷新，用于按使用频次排序）
    [ObservableProperty]
    private DateTime? lastUsedAt;

    // R8: 产线兼容标签，逗号分隔（如 "PRD-A,OP30" / "全工位"）
    [ObservableProperty]
    private string lineCompatibility = "全工位";

    // R9: 试运行模式（IsTrialRun=true 时不影响实际生产，仅记录）
    [ObservableProperty]
    private bool isTrialRun;

    [ObservableProperty]
    private int trialRunQuantity;

    // R6: 上次删除原因（删除前弹层填写，写入审计）— 不持久化（删除即被移除）
}
