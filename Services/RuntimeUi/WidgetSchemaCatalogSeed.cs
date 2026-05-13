#nullable enable
using System.Collections.Generic;
using ApexHMI.Models.RuntimeUi;

namespace ApexHMI.Services.RuntimeUi;

/// <summary>
/// P7.5C: 10 个高频 widget 的 Schema 种子数据。
/// </summary>
/// <remarks>
/// <para>已覆盖（10 个）：text / rectangle / ellipse / button / io-numeric / io-symbolic /
/// switch / bar / gauge / trend-view。</para>
///
/// <para><b>TODO P7.5C-续 — 中频 17 个 widget schema 待补：</b></para>
/// <list type="number">
///   <item>line — stroke/strokeThickness/strokeDashArray/x1,y1,x2,y2</item>
///   <item>polyline — points/stroke/strokeThickness/strokeDashArray/opacity</item>
///   <item>polygon — points/fill/stroke/strokeThickness/strokeDashArray/opacity</item>
///   <item>graphic-view — source(图片路径)/stretch(Enum)/opacity</item>
///   <item>io-graphic — variable/mode/entries(GraphicListRef)/stretch</item>
///   <item>datetime — mode(SystemTime/Tag)/variable/format/background/foreground</item>
///   <item>slider — variable/min/max/step/orientation/showLabel/showValue/snapToStep/writeOnChange</item>
///   <item>scrollbar — 同 slider</item>
///   <item>clock — mode(digital/analog)/format/foreground/background/fontSize/analogShowSeconds</item>
///   <item>combobox — variable/items(TextListRef 或字符串)</item>
///   <item>listbox — 同 combobox</item>
///   <item>checkbox — variable/text/checkedColor/uncheckedColor/foreground</item>
///   <item>optiongroup — variable/items/orientation</item>
///   <item>round-button — text/background/foreground/cornerRadius(由 width/2 接管)</item>
///   <item>alarm-view — filterCategory/columns(Json)/maxRows/showAck</item>
///   <item>table-view — columns(Json)/dataSource/showHeader/alternateRowColor</item>
///   <item>screen-window — pageRoute(PageRoute)/showHeader/zoomToFit</item>
/// </list>
/// 补全方式：在本类中追加 BuildXxx() 方法并在 Seed 里 Add；类型不确定时回退到 String。
/// </remarks>
internal static class WidgetSchemaCatalogSeed
{
    public static void Seed(Dictionary<string, WidgetSchema> map)
    {
        Add(map, BuildText());
        Add(map, BuildRectangle());
        Add(map, BuildEllipse());
        Add(map, BuildButton());
        Add(map, BuildIoNumeric());
        Add(map, BuildIoSymbolic());
        Add(map, BuildSwitch());
        Add(map, BuildBar());
        Add(map, BuildGauge());
        Add(map, BuildTrendView());

        // P8A 配方视图
        Add(map, BuildRecipeView());

        // P8B 用户视图
        Add(map, BuildUserView());

        // P8C 系统诊断视图
        Add(map, BuildDiagnosticView());

        // P8D 报警指示器
        Add(map, BuildAlarmIndicator());

        // P8E 状态强制
        Add(map, BuildStatusForce());
    }

    // ---------------- status-force (P8E) ----------------
    private static WidgetSchema BuildStatusForce() => new()
    {
        TypeId = "status-force",
        Properties = new[]
        {
            new PropertyDescriptor { Key = "tags", DisplayName = "Tag 列表", EditorType = PropertyEditorType.String, DefaultValue = "", Category = "数据", Description = "用逗号或分号分隔的 Tag 地址列表" },
            new PropertyDescriptor { Key = "readonly", DisplayName = "只读", EditorType = PropertyEditorType.Boolean, DefaultValue = "false", Category = "行为" },
            new PropertyDescriptor { Key = "background", DisplayName = "背景色", EditorType = PropertyEditorType.Color, DefaultValue = "#FFFFFF", Category = "外观" },
            new PropertyDescriptor { Key = "foreground", DisplayName = "前景色", EditorType = PropertyEditorType.Color, DefaultValue = "#0F172A", Category = "外观" },
        }
    };

    // ---------------- alarm-indicator (P8D) ----------------
    private static WidgetSchema BuildAlarmIndicator() => new()
    {
        TypeId = "alarm-indicator",
        Properties = new[]
        {
            new PropertyDescriptor { Key = "targetPage", DisplayName = "跳转页面", EditorType = PropertyEditorType.PageRoute, DefaultValue = "", Category = "行为" },
            new PropertyDescriptor
            {
                Key = "filterLevel", DisplayName = "过滤级别", EditorType = PropertyEditorType.Enum, DefaultValue = "All", Category = "数据",
                EnumOptions = new[] { "All|全部", "Info|信息", "Warning|警告", "Error|错误", "Alarm|报警" }
            },
            new PropertyDescriptor { Key = "blinkOnNew", DisplayName = "新报警闪烁", EditorType = PropertyEditorType.Boolean, DefaultValue = "true", Category = "行为" },
            new PropertyDescriptor { Key = "indicatorColor", DisplayName = "指示器颜色", EditorType = PropertyEditorType.Color, DefaultValue = "#DC2626", Category = "外观" },
            new PropertyDescriptor { Key = "foreground", DisplayName = "数字颜色", EditorType = PropertyEditorType.Color, DefaultValue = "#FFFFFF", Category = "外观" },
        }
    };

    // ---------------- diagnostic-view (P8C) ----------------
    private static WidgetSchema BuildDiagnosticView() => new()
    {
        TypeId = "diagnostic-view",
        Properties = new[]
        {
            new PropertyDescriptor { Key = "showCommSection", DisplayName = "显示通讯状态", EditorType = PropertyEditorType.Boolean, DefaultValue = "true", Category = "外观" },
            new PropertyDescriptor { Key = "showPlcSection",  DisplayName = "显示 PLC 状态", EditorType = PropertyEditorType.Boolean, DefaultValue = "true", Category = "外观" },
            new PropertyDescriptor { Key = "showHmiSection",  DisplayName = "显示 HMI 资源", EditorType = PropertyEditorType.Boolean, DefaultValue = "true", Category = "外观" },
            new PropertyDescriptor { Key = "refreshInterval", DisplayName = "刷新间隔(秒)", EditorType = PropertyEditorType.Number, DefaultValue = "1", Category = "数据" },
            new PropertyDescriptor { Key = "background",      DisplayName = "背景色", EditorType = PropertyEditorType.Color, DefaultValue = "#F8FAFC", Category = "外观" },
        }
    };

    // ---------------- user-view (P8B) ----------------
    private static WidgetSchema BuildUserView() => new()
    {
        TypeId = "user-view",
        Properties = new[]
        {
            new PropertyDescriptor { Key = "allowEdit", DisplayName = "允许修改", EditorType = PropertyEditorType.Boolean, DefaultValue = "true", Category = "行为" },
            new PropertyDescriptor { Key = "showLastLogin", DisplayName = "显示上次登录", EditorType = PropertyEditorType.Boolean, DefaultValue = "true", Category = "外观" },
            new PropertyDescriptor { Key = "background", DisplayName = "背景色", EditorType = PropertyEditorType.Color, DefaultValue = "#FFFFFF", Category = "外观" },
            new PropertyDescriptor { Key = "foreground", DisplayName = "前景色", EditorType = PropertyEditorType.Color, DefaultValue = "#0F172A", Category = "外观" },
        }
    };

    // ---------------- recipe-view (P8A) ----------------
    private static WidgetSchema BuildRecipeView() => new()
    {
        TypeId = "recipe-view",
        Properties = new[]
        {
            new PropertyDescriptor { Key = "recipeId", DisplayName = "配方", EditorType = PropertyEditorType.String, DefaultValue = "", Category = "数据", Description = "ProjectDocument.Recipes 中的 Recipe.Id" },
            new PropertyDescriptor { Key = "showToolbar", DisplayName = "显示工具栏", EditorType = PropertyEditorType.Boolean, DefaultValue = "true", Category = "外观" },
            new PropertyDescriptor { Key = "allowEditDataset", DisplayName = "允许编辑数据集", EditorType = PropertyEditorType.Boolean, DefaultValue = "true", Category = "行为" },
            new PropertyDescriptor { Key = "showFieldDescription", DisplayName = "显示字段描述", EditorType = PropertyEditorType.Boolean, DefaultValue = "false", Category = "外观" },
            new PropertyDescriptor { Key = "background", DisplayName = "背景色", EditorType = PropertyEditorType.Color, DefaultValue = "#FFFFFF", Category = "外观" },
            new PropertyDescriptor { Key = "foreground", DisplayName = "前景色", EditorType = PropertyEditorType.Color, DefaultValue = "#0F172A", Category = "外观" },
        }
    };

    private static void Add(Dictionary<string, WidgetSchema> map, WidgetSchema schema)
        => map[schema.TypeId] = schema;

    // ---------------- text ----------------
    private static WidgetSchema BuildText() => new()
    {
        TypeId = "text",
        Properties = new[]
        {
            new PropertyDescriptor { Key = "text", DisplayName = "文本", EditorType = PropertyEditorType.String, DefaultValue = "文本", Category = "文本" },
            new PropertyDescriptor { Key = "fontSize", DisplayName = "字号", EditorType = PropertyEditorType.Number, DefaultValue = "14", Category = "文本" },
            new PropertyDescriptor
            {
                Key = "fontWeight", DisplayName = "字重", EditorType = PropertyEditorType.Enum, DefaultValue = "Normal", Category = "文本",
                EnumOptions = new[] { "Normal|常规", "Bold|加粗", "SemiBold|半粗", "Light|细体" }
            },
            new PropertyDescriptor { Key = "foreground", DisplayName = "前景色", EditorType = PropertyEditorType.Color, DefaultValue = "#0F172A", Category = "外观" },
            new PropertyDescriptor { Key = "background", DisplayName = "背景色", EditorType = PropertyEditorType.Color, DefaultValue = "Transparent", Category = "外观" },
            new PropertyDescriptor
            {
                Key = "textAlign", DisplayName = "水平对齐", EditorType = PropertyEditorType.Enum, DefaultValue = "Left", Category = "文本",
                EnumOptions = new[] { "Left|左对齐", "Center|居中", "Right|右对齐" }
            },
            new PropertyDescriptor { Key = "padding", DisplayName = "内边距", EditorType = PropertyEditorType.Number, DefaultValue = "4", Category = "布局" },
        }
    };

    // ---------------- rectangle ----------------
    private static WidgetSchema BuildRectangle() => new()
    {
        TypeId = "rectangle",
        Properties = new[]
        {
            new PropertyDescriptor { Key = "fill", DisplayName = "填充色", EditorType = PropertyEditorType.Color, DefaultValue = "#3B82F6", Category = "外观" },
            new PropertyDescriptor { Key = "stroke", DisplayName = "描边色", EditorType = PropertyEditorType.Color, DefaultValue = "#1E40AF", Category = "外观" },
            new PropertyDescriptor { Key = "strokeThickness", DisplayName = "描边宽度", EditorType = PropertyEditorType.Number, DefaultValue = "1", Category = "外观" },
            new PropertyDescriptor { Key = "cornerRadius", DisplayName = "圆角半径", EditorType = PropertyEditorType.Number, DefaultValue = "0", Category = "外观" },
            new PropertyDescriptor { Key = "opacity", DisplayName = "不透明度", EditorType = PropertyEditorType.Number, DefaultValue = "1", Category = "外观", Description = "0~1" },
        }
    };

    // ---------------- ellipse ----------------
    private static WidgetSchema BuildEllipse() => new()
    {
        TypeId = "ellipse",
        Properties = new[]
        {
            new PropertyDescriptor { Key = "fill", DisplayName = "填充色", EditorType = PropertyEditorType.Color, DefaultValue = "#10B981", Category = "外观" },
            new PropertyDescriptor { Key = "stroke", DisplayName = "描边色", EditorType = PropertyEditorType.Color, DefaultValue = "#065F46", Category = "外观" },
            new PropertyDescriptor { Key = "strokeThickness", DisplayName = "描边宽度", EditorType = PropertyEditorType.Number, DefaultValue = "1", Category = "外观" },
            new PropertyDescriptor { Key = "opacity", DisplayName = "不透明度", EditorType = PropertyEditorType.Number, DefaultValue = "1", Category = "外观" },
        }
    };

    // ---------------- button ----------------
    private static WidgetSchema BuildButton() => new()
    {
        TypeId = "button",
        Properties = new[]
        {
            new PropertyDescriptor { Key = "text", DisplayName = "按钮文本", EditorType = PropertyEditorType.String, DefaultValue = "按钮", Category = "文本" },
            new PropertyDescriptor { Key = "fontSize", DisplayName = "字号", EditorType = PropertyEditorType.Number, DefaultValue = "14", Category = "文本" },
            new PropertyDescriptor { Key = "foreground", DisplayName = "文字颜色", EditorType = PropertyEditorType.Color, DefaultValue = "#FFFFFF", Category = "外观" },
            new PropertyDescriptor { Key = "background", DisplayName = "背景色", EditorType = PropertyEditorType.Color, DefaultValue = "#2563EB", Category = "外观" },
            new PropertyDescriptor { Key = "borderColor", DisplayName = "边框色", EditorType = PropertyEditorType.Color, DefaultValue = "#1D4ED8", Category = "外观" },
            new PropertyDescriptor { Key = "borderWidth", DisplayName = "边框宽度", EditorType = PropertyEditorType.Number, DefaultValue = "1", Category = "外观" },
            new PropertyDescriptor { Key = "cornerRadius", DisplayName = "圆角", EditorType = PropertyEditorType.Number, DefaultValue = "4", Category = "外观" },
            new PropertyDescriptor { Key = "pressedBackground", DisplayName = "按下背景色", EditorType = PropertyEditorType.Color, DefaultValue = "#1E40AF", Category = "外观" },
        }
    };

    // ---------------- io-numeric ----------------
    private static WidgetSchema BuildIoNumeric() => new()
    {
        TypeId = "io-numeric",
        Properties = new[]
        {
            new PropertyDescriptor { Key = "variable", DisplayName = "变量", EditorType = PropertyEditorType.TagAddress, DefaultValue = "", Category = "数据" },
            new PropertyDescriptor
            {
                Key = "mode", DisplayName = "模式", EditorType = PropertyEditorType.Enum, DefaultValue = "Output", Category = "数据",
                EnumOptions = new[] { "Input|输入", "Output|输出", "InputOutput|输入输出" }
            },
            new PropertyDescriptor { Key = "format", DisplayName = "数值格式", EditorType = PropertyEditorType.String, DefaultValue = "0.##", Category = "格式", Description = ".NET 数值格式串" },
            new PropertyDescriptor { Key = "decimals", DisplayName = "小数位", EditorType = PropertyEditorType.Integer, DefaultValue = "2", Category = "格式" },
            new PropertyDescriptor { Key = "unit", DisplayName = "单位", EditorType = PropertyEditorType.String, DefaultValue = "", Category = "格式" },
            new PropertyDescriptor { Key = "minValue", DisplayName = "最小值", EditorType = PropertyEditorType.Number, DefaultValue = "", Category = "限值" },
            new PropertyDescriptor { Key = "maxValue", DisplayName = "最大值", EditorType = PropertyEditorType.Number, DefaultValue = "", Category = "限值" },
            new PropertyDescriptor { Key = "background", DisplayName = "背景色", EditorType = PropertyEditorType.Color, DefaultValue = "#FFFFFF", Category = "外观" },
            new PropertyDescriptor { Key = "foreground", DisplayName = "前景色", EditorType = PropertyEditorType.Color, DefaultValue = "#0F172A", Category = "外观" },
            new PropertyDescriptor { Key = "fontSize", DisplayName = "字号", EditorType = PropertyEditorType.Number, DefaultValue = "14", Category = "文本" },
            new PropertyDescriptor
            {
                Key = "textAlign", DisplayName = "水平对齐", EditorType = PropertyEditorType.Enum, DefaultValue = "Right", Category = "文本",
                EnumOptions = new[] { "Left|左对齐", "Center|居中", "Right|右对齐" }
            },
        }
    };

    // ---------------- io-symbolic ----------------
    private static WidgetSchema BuildIoSymbolic() => new()
    {
        TypeId = "io-symbolic",
        Properties = new[]
        {
            new PropertyDescriptor { Key = "variable", DisplayName = "变量", EditorType = PropertyEditorType.TagAddress, DefaultValue = "", Category = "数据" },
            new PropertyDescriptor
            {
                Key = "mode", DisplayName = "模式", EditorType = PropertyEditorType.Enum, DefaultValue = "Output", Category = "数据",
                EnumOptions = new[] { "Input|输入", "Output|输出", "InputOutput|输入输出" }
            },
            new PropertyDescriptor { Key = "entries", DisplayName = "条目映射", EditorType = PropertyEditorType.String, DefaultValue = "0=停止;1=运行", Category = "数据", Description = "格式：value=text;value=text，或引用文本列表" },
            new PropertyDescriptor { Key = "background", DisplayName = "背景色", EditorType = PropertyEditorType.Color, DefaultValue = "#FFFFFF", Category = "外观" },
            new PropertyDescriptor { Key = "foreground", DisplayName = "前景色", EditorType = PropertyEditorType.Color, DefaultValue = "#0F172A", Category = "外观" },
        }
    };

    // ---------------- switch ----------------
    private static WidgetSchema BuildSwitch() => new()
    {
        TypeId = "switch",
        Properties = new[]
        {
            new PropertyDescriptor { Key = "variable", DisplayName = "变量", EditorType = PropertyEditorType.TagAddress, DefaultValue = "", Category = "数据" },
            new PropertyDescriptor
            {
                Key = "mode", DisplayName = "模式", EditorType = PropertyEditorType.Enum, DefaultValue = "bistable", Category = "行为",
                EnumOptions = new[] { "bistable|双稳态", "momentary|点动" }
            },
            new PropertyDescriptor { Key = "onText", DisplayName = "ON 文本", EditorType = PropertyEditorType.String, DefaultValue = "ON", Category = "文本" },
            new PropertyDescriptor { Key = "offText", DisplayName = "OFF 文本", EditorType = PropertyEditorType.String, DefaultValue = "OFF", Category = "文本" },
            new PropertyDescriptor { Key = "onColor", DisplayName = "ON 颜色", EditorType = PropertyEditorType.Color, DefaultValue = "#10B981", Category = "外观" },
            new PropertyDescriptor { Key = "offColor", DisplayName = "OFF 颜色", EditorType = PropertyEditorType.Color, DefaultValue = "#94A3B8", Category = "外观" },
            new PropertyDescriptor
            {
                Key = "orientation", DisplayName = "方向", EditorType = PropertyEditorType.Enum, DefaultValue = "horizontal", Category = "布局",
                EnumOptions = new[] { "horizontal|水平", "vertical|垂直" }
            },
        }
    };

    // ---------------- bar ----------------
    private static WidgetSchema BuildBar() => new()
    {
        TypeId = "bar",
        Properties = new[]
        {
            new PropertyDescriptor { Key = "variable", DisplayName = "变量", EditorType = PropertyEditorType.TagAddress, DefaultValue = "", Category = "数据" },
            new PropertyDescriptor { Key = "minValue", DisplayName = "最小值", EditorType = PropertyEditorType.Number, DefaultValue = "0", Category = "限值" },
            new PropertyDescriptor { Key = "maxValue", DisplayName = "最大值", EditorType = PropertyEditorType.Number, DefaultValue = "100", Category = "限值" },
            new PropertyDescriptor
            {
                Key = "orientation", DisplayName = "方向", EditorType = PropertyEditorType.Enum, DefaultValue = "vertical", Category = "布局",
                EnumOptions = new[] { "horizontal|水平", "vertical|垂直" }
            },
            new PropertyDescriptor { Key = "fillColor", DisplayName = "填充色", EditorType = PropertyEditorType.Color, DefaultValue = "#3B82F6", Category = "外观" },
            new PropertyDescriptor { Key = "backgroundColor", DisplayName = "背景色", EditorType = PropertyEditorType.Color, DefaultValue = "#E5E7EB", Category = "外观" },
            new PropertyDescriptor { Key = "warnThreshold", DisplayName = "警告阈值", EditorType = PropertyEditorType.Number, DefaultValue = "", Category = "限值" },
            new PropertyDescriptor { Key = "warnColor", DisplayName = "警告色", EditorType = PropertyEditorType.Color, DefaultValue = "#F59E0B", Category = "外观" },
            new PropertyDescriptor { Key = "alarmThreshold", DisplayName = "报警阈值", EditorType = PropertyEditorType.Number, DefaultValue = "", Category = "限值" },
            new PropertyDescriptor { Key = "alarmColor", DisplayName = "报警色", EditorType = PropertyEditorType.Color, DefaultValue = "#EF4444", Category = "外观" },
            new PropertyDescriptor { Key = "showLabel", DisplayName = "显示标签", EditorType = PropertyEditorType.Boolean, DefaultValue = "true", Category = "外观" },
            new PropertyDescriptor { Key = "showScale", DisplayName = "显示刻度", EditorType = PropertyEditorType.Boolean, DefaultValue = "false", Category = "外观" },
        }
    };

    // ---------------- gauge ----------------
    private static WidgetSchema BuildGauge() => new()
    {
        TypeId = "gauge",
        Properties = new[]
        {
            new PropertyDescriptor { Key = "variable", DisplayName = "变量", EditorType = PropertyEditorType.TagAddress, DefaultValue = "", Category = "数据" },
            new PropertyDescriptor { Key = "minValue", DisplayName = "最小值", EditorType = PropertyEditorType.Number, DefaultValue = "0", Category = "限值" },
            new PropertyDescriptor { Key = "maxValue", DisplayName = "最大值", EditorType = PropertyEditorType.Number, DefaultValue = "100", Category = "限值" },
            new PropertyDescriptor { Key = "unit", DisplayName = "单位", EditorType = PropertyEditorType.String, DefaultValue = "", Category = "格式" },
            new PropertyDescriptor { Key = "warnThreshold", DisplayName = "警告阈值", EditorType = PropertyEditorType.Number, DefaultValue = "", Category = "限值" },
            new PropertyDescriptor { Key = "warnColor", DisplayName = "警告色", EditorType = PropertyEditorType.Color, DefaultValue = "#F59E0B", Category = "外观" },
            new PropertyDescriptor { Key = "alarmThreshold", DisplayName = "报警阈值", EditorType = PropertyEditorType.Number, DefaultValue = "", Category = "限值" },
            new PropertyDescriptor { Key = "alarmColor", DisplayName = "报警色", EditorType = PropertyEditorType.Color, DefaultValue = "#EF4444", Category = "外观" },
            new PropertyDescriptor { Key = "majorTicks", DisplayName = "主刻度数", EditorType = PropertyEditorType.Integer, DefaultValue = "10", Category = "外观" },
            new PropertyDescriptor { Key = "minorTicks", DisplayName = "次刻度数", EditorType = PropertyEditorType.Integer, DefaultValue = "5", Category = "外观" },
            new PropertyDescriptor { Key = "foreground", DisplayName = "指针色", EditorType = PropertyEditorType.Color, DefaultValue = "#2563EB", Category = "外观" },
        }
    };

    // ---------------- trend-view ----------------
    private static WidgetSchema BuildTrendView() => new()
    {
        TypeId = "trend-view",
        Properties = new[]
        {
            new PropertyDescriptor { Key = "traces", DisplayName = "曲线配置", EditorType = PropertyEditorType.Json, DefaultValue = "[]", Category = "数据", Description = "JSON 数组，每项 {tag, color, label}" },
            new PropertyDescriptor
            {
                Key = "mode", DisplayName = "模式", EditorType = PropertyEditorType.Enum, DefaultValue = "realtime", Category = "数据",
                EnumOptions = new[] { "realtime|实时", "history|历史" }
            },
            new PropertyDescriptor { Key = "timeWindow", DisplayName = "时间窗口(秒)", EditorType = PropertyEditorType.Number, DefaultValue = "60", Category = "数据" },
            new PropertyDescriptor { Key = "yMin", DisplayName = "Y 最小值", EditorType = PropertyEditorType.Number, DefaultValue = "0", Category = "限值" },
            new PropertyDescriptor { Key = "yMax", DisplayName = "Y 最大值", EditorType = PropertyEditorType.Number, DefaultValue = "100", Category = "限值" },
            new PropertyDescriptor { Key = "showLegend", DisplayName = "显示图例", EditorType = PropertyEditorType.Boolean, DefaultValue = "true", Category = "外观" },
            new PropertyDescriptor { Key = "showGrid", DisplayName = "显示网格", EditorType = PropertyEditorType.Boolean, DefaultValue = "true", Category = "外观" },
            new PropertyDescriptor { Key = "showToolbar", DisplayName = "显示工具栏", EditorType = PropertyEditorType.Boolean, DefaultValue = "false", Category = "外观" },
            new PropertyDescriptor { Key = "backgroundColor", DisplayName = "背景色", EditorType = PropertyEditorType.Color, DefaultValue = "#FFFFFF", Category = "外观" },
        }
    };
}
