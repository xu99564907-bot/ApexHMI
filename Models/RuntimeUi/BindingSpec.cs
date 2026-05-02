namespace ApexHMI.Models.RuntimeUi;

/// <summary>逻辑数据点绑定描述。页面只引用逻辑 TagId，不直接依赖 OPC UA NodeId。</summary>
public class BindingSpec
{
    /// <summary>逻辑 Tag 名，对应 Tags 表中的 TagItem.Name。</summary>
    public string TagId { get; set; } = string.Empty;

    /// <summary>访问模式：订阅/只读/读写/仅写。</summary>
    public BindingAccessMode AccessMode { get; set; } = BindingAccessMode.Subscribe;

    /// <summary>期望数据类型，用于格式化显示（String/Bool/Int/Float）。</summary>
    public string DataType { get; set; } = "String";
}

public enum BindingAccessMode
{
    Subscribe,
    ReadOnly,
    ReadWrite,
    WriteOnly
}
