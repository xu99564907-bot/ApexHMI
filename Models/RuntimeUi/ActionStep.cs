#nullable enable
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ApexHMI.Models.RuntimeUi;

/// <summary>P1: 事件触发后顺序执行的一个动作步骤。
/// <para>FunctionId 对应 <see cref="ApexHMI.Services.RuntimeUi.SystemFunctionCatalog"/> 中的函数 ID。</para>
/// <para>Args 是命名参数表，按函数定义填写（如 address/value/routeKey/text）。</para>
/// </summary>
public partial class ActionStep : ObservableObject
{
    [ObservableProperty] private string _functionId = string.Empty;

    /// <summary>命名参数表。Key 与 SystemFunction.Args[*].Key 对应。</summary>
    public Dictionary<string, string> Args { get; set; } = new();
}
