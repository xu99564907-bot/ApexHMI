using ApexHMI.Models.RuntimeUi;

namespace ApexHMI.Services.RuntimeUi;

/// <summary>控件编辑器服务：增删控件、修改属性/绑定/位置/尺寸。</summary>
public interface IWidgetEditorService
{
    /// <summary>在指定页面上添加指定类型的控件，自动生成 Id 和默认属性。</summary>
    WidgetInstance AddWidget(PageDefinition page, string typeId, double x, double y);

    /// <summary>删除指定 Id 的控件。</summary>
    bool RemoveWidget(PageDefinition page, string widgetId);

    /// <summary>修改控件属性字典中的单个 key 值。若 key 已存在则覆盖，否则新增。</summary>
    void UpdateProperty(WidgetInstance widget, string key, string? value);

    /// <summary>设置或清空控件的数据绑定。</summary>
    void UpdateBinding(WidgetInstance widget, BindingSpec? binding);

    /// <summary>移动控件到新坐标。</summary>
    void MoveWidget(WidgetInstance widget, double x, double y);

    /// <summary>调整控件尺寸。</summary>
    void ResizeWidget(WidgetInstance widget, double width, double height);
}
