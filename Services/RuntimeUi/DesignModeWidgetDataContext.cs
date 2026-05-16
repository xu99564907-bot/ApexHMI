using System;
using System.Collections.Generic;
using ApexHMI.Models.RuntimeUi;

namespace ApexHMI.Services.RuntimeUi;

/// <summary>
/// 设计模式下的 IWidgetDataContext 实现：
/// - 不订阅 OPC UA Tag 回调（设计器无 Tag 推送循环）
/// - 不执行 widget 动作（点击 widget 在设计模式下应用于"选中"，不应触发 navigate/write 等运行行为）
/// - 暴露 Shell 供业务 widget（manual-cylinder-block 等）从中读取真实数据，
///   实现设计模式画布上的业务控件 WYSIWYG 预览。
/// </summary>
public sealed class DesignModeWidgetDataContext : IWidgetDataContext
{
    public DesignModeWidgetDataContext(object? shell = null)
    {
        Shell = shell;
    }

    public object? Shell { get; }

    /// <summary>P7B: 顶层设计画布无 Faceplate 上下文。</summary>
    public IReadOnlyDictionary<string, string>? CurrentFaceplateProperties => null;

    public void RegisterValueCallback(string tagId, Action<string> callback)
    {
        // 设计模式无实时数据
    }

    /// <summary>M3.1: 设计模式无实时质量推送。</summary>
    public void RegisterValueCallback(string tagId, Action<string, TagQuality> callback)
    {
        // 设计模式无实时数据
    }

    public void ExecuteAction(string actionType, string actionParam)
    {
        // 设计模式禁用运行时动作
    }
}
