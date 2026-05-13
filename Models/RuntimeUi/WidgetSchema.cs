#nullable enable
using System.Collections.Generic;

namespace ApexHMI.Models.RuntimeUi;

/// <summary>
/// P7.5: 属性编辑器类型枚举 — 决定属性面板中该字段渲染什么 UI。
/// <para>面板通过 <see cref="PropertyEditorTemplateSelector"/> 把这个枚举映射到对应 DataTemplate。</para>
/// </summary>
public enum PropertyEditorType
{
    /// <summary>单行文本（TextBox）。</summary>
    String,
    /// <summary>多行文本（多行 TextBox + 滚动条）。</summary>
    MultilineString,
    /// <summary>数字（double）— TextBox + 数字格式校验。</summary>
    Number,
    /// <summary>整数 — TextBox + 整数校验。</summary>
    Integer,
    /// <summary>布尔 — CheckBox。</summary>
    Boolean,
    /// <summary>颜色 — 色块预览 + hex 输入 + Popup 取色器。</summary>
    Color,
    /// <summary>OPC UA Tag 地址 — TextBox + TagAutoComplete。</summary>
    TagAddress,
    /// <summary>下拉枚举 — ComboBox + EnumOptions。</summary>
    Enum,
    /// <summary>页面 RouteKey — ComboBox 列出工程内页面 + 固定段名。</summary>
    PageRoute,
    /// <summary>JSON — 多行 TextBox（v2.0 升级语法高亮）。</summary>
    Json,
    /// <summary>文本列表引用（指向 ListResources.TextLists）。</summary>
    TextListRef,
    /// <summary>图形列表引用（指向 ListResources.GraphicLists）。</summary>
    GraphicListRef,
    /// <summary>字体选择（v2.0；当前回退到 String）。</summary>
    Font,
}

/// <summary>
/// P7.5: 单条属性的 Schema 描述（类型、显示名、默认值、分组、枚举选项等）。
/// </summary>
public sealed record PropertyDescriptor
{
    /// <summary>属性 key（对应 WidgetInstance.Properties 字典 key）。</summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>中文显示名（属性面板第一列）。</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>编辑器类型。</summary>
    public PropertyEditorType EditorType { get; init; } = PropertyEditorType.String;

    /// <summary>默认值（创建 widget 时自动填入 Properties）。</summary>
    public string DefaultValue { get; init; } = string.Empty;

    /// <summary>分类（按此分组到 Expander）：常规 / 外观 / 布局 / 文本 / 格式 / 限值 / 数据 / 行为 / 高级 等。</summary>
    public string Category { get; init; } = "常规";

    /// <summary>Tooltip 提示，留空时显示 Key。</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>枚举类型时的选项列表（value=Key，display=Key 或自带显示），格式：value|label。
    /// 例如 fontWeight: ["Normal|常规","Bold|加粗","SemiBold|半粗"]。</summary>
    public IReadOnlyList<string>? EnumOptions { get; init; }
}

/// <summary>
/// P7.5: 单个 widget 类型的属性 Schema（属性列表 + TypeId）。
/// </summary>
public sealed record WidgetSchema
{
    /// <summary>对应 WidgetInstance.TypeId（如 "text"/"button"/"bar"）。</summary>
    public string TypeId { get; init; } = string.Empty;

    /// <summary>该 widget 的属性描述列表（顺序即面板显示顺序）。</summary>
    public IReadOnlyList<PropertyDescriptor> Properties { get; init; } = new List<PropertyDescriptor>();
}
