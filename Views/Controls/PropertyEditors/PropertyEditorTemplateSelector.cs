#nullable enable
using System.Windows;
using System.Windows.Controls;
using ApexHMI.Models.RuntimeUi;
using ApexHMI.ViewModels.Modules;

namespace ApexHMI.Views.Controls.PropertyEditors;

/// <summary>
/// P7.5: 根据 <see cref="PropertyEditorVM.EditorType"/> 选择对应 DataTemplate。
/// 各模板在 PropertyEditorTemplates.xaml 中定义，Key 为
/// "PropEditor.<see cref="PropertyEditorType"/> 名称"。
/// </summary>
public sealed class PropertyEditorTemplateSelector : DataTemplateSelector
{
    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
    {
        if (item is not PropertyEditorVM vm) return base.SelectTemplate(item, container);
        if (container is not FrameworkElement fe) return base.SelectTemplate(item, container);

        var key = "PropEditor." + vm.EditorType;
        if (fe.TryFindResource(key) is DataTemplate tmpl) return tmpl;

        // 回退：String
        if (fe.TryFindResource("PropEditor.String") is DataTemplate fallback) return fallback;
        return base.SelectTemplate(item, container);
    }
}
