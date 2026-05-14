#nullable enable
using System;
using System.Collections.Generic;
using ApexHMI.Models.RuntimeUi;

namespace ApexHMI.Services.RuntimeUi;

/// <summary>
/// P7B: Faceplate 实例 InnerScreen 内部渲染时使用的数据上下文。
/// <para>大部分调用透传给父上下文（Tag 订阅、动作执行、Shell 访问）；
/// 唯独 <see cref="CurrentFaceplateProperties"/> 返回当前 Faceplate 实例的接口属性快照，
/// 供内部 widget 通过 <see cref="FaceplateResolver"/> 解析 <c>{prop:keyName}</c>。</para>
/// </summary>
public sealed class FaceplateChildDataContext : IWidgetDataContext
{
    private readonly IWidgetDataContext _parent;
    private readonly IReadOnlyDictionary<string, string> _propertyValues;

    public FaceplateChildDataContext(IWidgetDataContext parent, IReadOnlyDictionary<string, string> propertyValues)
    {
        _parent = parent;
        _propertyValues = propertyValues;
    }

    public object? Shell => _parent.Shell;

    public IReadOnlyDictionary<string, string>? CurrentFaceplateProperties => _propertyValues;

    public void RegisterValueCallback(string tagId, Action<string> callback)
        => _parent.RegisterValueCallback(tagId, callback);

    /// <summary>M3.1: 透传 quality 回调给父上下文。</summary>
    public void RegisterValueCallback(string tagId, Action<string, TagQuality> callback)
        => _parent.RegisterValueCallback(tagId, callback);

    public void ExecuteAction(string actionType, string actionParam)
        => _parent.ExecuteAction(actionType, actionParam);
}
