using System;

namespace ApexHMI.Services.RuntimeUi;

/// <summary>
/// 设计模式下的 IWidgetDataContext 实现：
/// - 不订阅 OPC UA Tag 回调（设计器无运行数据）
/// - 不执行 widget 动作（点击 widget 在设计模式下应用于"选中"，不应触发 navigate/write 等运行行为）
/// </summary>
public sealed class DesignModeWidgetDataContext : IWidgetDataContext
{
    public void RegisterValueCallback(string tagId, Action<string> callback)
    {
        // 设计模式无实时数据
    }

    public void ExecuteAction(string actionType, string actionParam)
    {
        // 设计模式禁用运行时动作
    }
}
