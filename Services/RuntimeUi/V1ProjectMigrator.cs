using System;
using System.Collections.Generic;
using System.Linq;
using ApexHMI.Models;
using ApexHMI.Models.RuntimeUi;

namespace ApexHMI.Services.RuntimeUi;

/// <summary>
/// V1 DesignerProject → Phase A ProjectDocument 格式迁移器。
/// 映射规则见 docs/开放平台开发文档.md §5.3 及 §1.10。
/// </summary>
public static class V1ProjectMigrator
{
    /// <summary>
    /// V1 控件类型 → Phase A TypeId 映射表。
    /// indicator ≈ bool-lamp, value-display ≈ numeric-readonly, page-button ≈ button 可在 WidgetRegistry 直接映射。
    /// </summary>
    private static readonly Dictionary<string, string> TypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Button"] = "button",
        ["Label"] = "text",
        ["Indicator"] = "bool-lamp",
        ["ValueDisplay"] = "numeric-readonly",
        ["Motor"] = "motor",
        ["Cylinder"] = "cylinder",
        ["Axis"] = "axis",
        ["Robot"] = "robot",
        ["Stopper"] = "stopper",
        ["AlarmBanner"] = "alarm-banner",
        ["PageButton"] = "button",
    };

    /// <summary>
    /// 将 V1 DesignerProject 整体转换为 Phase A ProjectDocument。
    /// </summary>
    public static ProjectDocument MigrateProject(DesignerProject v1Project)
    {
        var doc = new ProjectDocument
        {
            SchemaVersion = 1,
            ProjectName = v1Project.ProjectName,
            Pages = new List<PageDefinition>()
        };

        foreach (var v1Page in v1Project.Pages)
        {
            doc.Pages.Add(MigratePage(v1Page));
        }

        doc.DefaultPageRouteKey = doc.Pages.FirstOrDefault()?.RouteKey;
        return doc;
    }

    /// <summary>
    /// 将单个 V1 DesignerPage 转换为 Phase A PageDefinition。
    /// RouteKey 取自 Name 的小写形式，中文名保留为 Title。
    /// </summary>
    private static PageDefinition MigratePage(DesignerPage v1Page)
    {
        var routeKey = SanitizeRouteKey(v1Page.Name);
        var page = new PageDefinition
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = v1Page.Name,
            RouteKey = routeKey,
            CanvasWidth = v1Page.CanvasWidth,
            CanvasHeight = v1Page.CanvasHeight,
            Widgets = new List<WidgetInstance>()
        };

        foreach (var v1Element in v1Page.Elements)
        {
            var widget = MigrateElement(v1Element);
            if (widget is not null)
            {
                page.Widgets.Add(widget);
            }
        }

        return page;
    }

    /// <summary>
    /// 将单个 V1 DesignerElement 转换为 Phase A WidgetInstance。
    /// </summary>
    private static WidgetInstance? MigrateElement(DesignerElement v1Element)
    {
        if (!TypeMap.TryGetValue(v1Element.ElementType, out var phaseTypeId))
        {
            // 未知类型跳过（保留 V1 的容错性）
            return null;
        }

        var widget = new WidgetInstance
        {
            Id = v1Element.Id,
            TypeId = phaseTypeId,
            X = v1Element.Left,
            Y = v1Element.Top,
            Width = v1Element.Width,
            Height = v1Element.Height,
            Properties = BuildProperties(v1Element),
            Binding = BuildBinding(v1Element),
        };

        BuildAction(v1Element, phaseTypeId, widget);
        return widget;
    }

    private static Dictionary<string, string> BuildProperties(DesignerElement e)
    {
        var props = new Dictionary<string, string>();

        if (!string.IsNullOrWhiteSpace(e.Text))
        {
            props["text"] = e.Text;
        }

        if (!string.IsNullOrWhiteSpace(e.Background))
        {
            props["background"] = e.Background;
        }

        if (!string.IsNullOrWhiteSpace(e.Foreground))
        {
            props["foreground"] = e.Foreground;
        }

        if (!string.IsNullOrWhiteSpace(e.BorderBrush))
        {
            props["borderBrush"] = e.BorderBrush;
        }

        if (e.FontSize > 0)
        {
            props["fontSize"] = e.FontSize.ToString();
        }

        return props;
    }

    private static BindingSpec? BuildBinding(DesignerElement e)
    {
        if (string.IsNullOrWhiteSpace(e.TagBinding))
        {
            return null;
        }

        var accessMode = BindingAccessMode.Subscribe;

        if (!string.IsNullOrWhiteSpace(e.CommandBinding)
            && e.CommandBinding.Contains("ToggleBool", StringComparison.OrdinalIgnoreCase))
        {
            accessMode = BindingAccessMode.ReadWrite;
        }

        // 推断 DataType：根据控件类型猜测（实际类型由 OPC UA 决定，此处给提示值）
        var dataType = e.ElementType switch
        {
            "Indicator" or "Motor" or "Cylinder" or "Robot" or "Stopper" or "AlarmBanner" => "Bool",
            "ValueDisplay" => "Float",
            _ => "String"
        };

        return new BindingSpec
        {
            TagId = e.TagBinding,
            AccessMode = accessMode,
            DataType = dataType
        };
    }

    private static void BuildAction(DesignerElement e, string phaseTypeId, WidgetInstance widget)
    {
        // PageButton → navigate 动作
        if (e.ElementType.Equals("PageButton", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(e.NavigationTarget))
        {
            widget.ActionType = "navigate";
            widget.ActionParam = SanitizeRouteKey(e.NavigationTarget);
            return;
        }

        // 有 ToggleBool 命令的 Button → write-bool 动作
        if (e.ElementType is "Button" or "Motor" or "Cylinder" or "Robot" or "Stopper"
            && !string.IsNullOrWhiteSpace(e.CommandBinding)
            && !string.IsNullOrWhiteSpace(e.TagBinding))
        {
            widget.ActionType = "write-bool";
            widget.ActionParam = $"{e.TagBinding}|True";
        }
    }

    /// <summary>
    /// 将中文页面名转换为英文 RouteKey，用于页面导航。
    /// 简单规则：取拼音首字母或直接用预设映射表。
    /// </summary>
    private static string SanitizeRouteKey(string name)
    {
        return name switch
        {
            "主界面" or "首页" => "main",
            "监控画面" or "监控" => "monitor",
            "手动画面" or "手动操作" or "手动" => "manual",
            "参数设定" or "参数" => "parameter",
            "配方管理" or "配方" => "recipe",
            "报警画面" or "报警" => "alarm",
            "登录" or "登入" => "login",
            "设计器" or "设计" => "designer",
            _ => name.ToLowerInvariant().Replace(" ", "-")
        };
    }
}
