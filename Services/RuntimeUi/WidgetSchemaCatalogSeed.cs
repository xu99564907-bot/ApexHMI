#nullable enable
using System.Collections.Generic;
using System.Linq;
using ApexHMI.Models.RuntimeUi;

namespace ApexHMI.Services.RuntimeUi;

/// <summary>
/// P7.5C / P10A: Widget Schema 种子数据（已覆盖 27 个）。
/// </summary>
/// <remarks>
/// 高频 10：text / rectangle / ellipse / button / io-numeric / io-symbolic / switch / bar / gauge / trend-view。
/// 业务类 11：recipe-view / user-view / diagnostic-view / alarm-indicator / status-force /
/// html-browser / pdf-view / media-player / xy-trend / report-view / 待补少量。
/// 中频 17 (P10A 补全)：line / polyline / polygon / graphic-view / io-graphic / datetime /
/// slider / scrollbar / clock / combobox / listbox / checkbox / optiongroup / round-button /
/// alarm-view / table-view / screen-window。
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

        // P9 媒体/分析类高级控件
        Add(map, BuildHtmlBrowser());
        Add(map, BuildPdfView());
        Add(map, BuildMediaPlayer());
        Add(map, BuildXyTrend());
        Add(map, BuildReportView());

        // P10A 中频 17 个 widget schema
        Add(map, BuildLine());
        Add(map, BuildPolyline());
        Add(map, BuildPolygon());
        Add(map, BuildGraphicView());
        Add(map, BuildIoGraphic());
        Add(map, BuildDateTime());
        Add(map, BuildSlider());
        Add(map, BuildScrollbar());
        Add(map, BuildClock());
        Add(map, BuildCombobox());
        Add(map, BuildListbox());
        Add(map, BuildCheckbox());
        Add(map, BuildOptionGroup());
        Add(map, BuildRoundButton());
        Add(map, BuildAlarmView());
        Add(map, BuildTableView());
        Add(map, BuildScreenWindow());
    }

    // ================= P10A 中频 17 widget schema =================

    private static WidgetSchema BuildLine() => new()
    {
        TypeId = "line",
        Properties = new[]
        {
            new PropertyDescriptor { Key = "stroke", DisplayName = "描边色", EditorType = PropertyEditorType.Color, DefaultValue = "#1F2937", Category = "外观" },
            new PropertyDescriptor { Key = "strokeThickness", DisplayName = "描边宽度", EditorType = PropertyEditorType.Number, DefaultValue = "2", Category = "外观" },
            new PropertyDescriptor { Key = "strokeDashArray", DisplayName = "虚线段", EditorType = PropertyEditorType.String, DefaultValue = "", Category = "外观", Description = "例 '4,2'" },
            new PropertyDescriptor { Key = "x1", DisplayName = "起点 X (相对)", EditorType = PropertyEditorType.Number, DefaultValue = "0", Category = "布局" },
            new PropertyDescriptor { Key = "y1", DisplayName = "起点 Y (相对)", EditorType = PropertyEditorType.Number, DefaultValue = "0", Category = "布局" },
            new PropertyDescriptor { Key = "x2", DisplayName = "终点 X (相对)", EditorType = PropertyEditorType.Number, DefaultValue = "1", Category = "布局" },
            new PropertyDescriptor { Key = "y2", DisplayName = "终点 Y (相对)", EditorType = PropertyEditorType.Number, DefaultValue = "1", Category = "布局" },
        }
    };

    private static WidgetSchema BuildPolyline() => new()
    {
        TypeId = "polyline",
        Properties = new[]
        {
            new PropertyDescriptor { Key = "points", DisplayName = "点列表", EditorType = PropertyEditorType.String, DefaultValue = "0,0 60,40 120,0", Category = "布局", Description = "'x1,y1 x2,y2 ...'" },
            new PropertyDescriptor { Key = "stroke", DisplayName = "描边色", EditorType = PropertyEditorType.Color, DefaultValue = "#1F2937", Category = "外观" },
            new PropertyDescriptor { Key = "strokeThickness", DisplayName = "描边宽度", EditorType = PropertyEditorType.Number, DefaultValue = "2", Category = "外观" },
            new PropertyDescriptor { Key = "strokeDashArray", DisplayName = "虚线段", EditorType = PropertyEditorType.String, DefaultValue = "", Category = "外观" },
            new PropertyDescriptor { Key = "opacity", DisplayName = "不透明度", EditorType = PropertyEditorType.Number, DefaultValue = "1", Category = "外观" },
        }
    };

    private static WidgetSchema BuildPolygon() => new()
    {
        TypeId = "polygon",
        Properties = new[]
        {
            new PropertyDescriptor { Key = "points", DisplayName = "点列表", EditorType = PropertyEditorType.String, DefaultValue = "60,0 120,60 60,120 0,60", Category = "布局" },
            new PropertyDescriptor { Key = "fill", DisplayName = "填充色", EditorType = PropertyEditorType.Color, DefaultValue = "#F59E0B", Category = "外观" },
            new PropertyDescriptor { Key = "stroke", DisplayName = "描边色", EditorType = PropertyEditorType.Color, DefaultValue = "#92400E", Category = "外观" },
            new PropertyDescriptor { Key = "strokeThickness", DisplayName = "描边宽度", EditorType = PropertyEditorType.Number, DefaultValue = "1", Category = "外观" },
            new PropertyDescriptor { Key = "strokeDashArray", DisplayName = "虚线段", EditorType = PropertyEditorType.String, DefaultValue = "", Category = "外观" },
            new PropertyDescriptor { Key = "opacity", DisplayName = "不透明度", EditorType = PropertyEditorType.Number, DefaultValue = "1", Category = "外观" },
        }
    };

    private static WidgetSchema BuildGraphicView() => new()
    {
        TypeId = "graphic-view",
        Properties = new[]
        {
            new PropertyDescriptor { Key = "source", DisplayName = "图片路径", EditorType = PropertyEditorType.String, DefaultValue = "", Category = "数据", Description = "本地路径或 pack:// URI" },
            new PropertyDescriptor
            {
                Key = "stretch", DisplayName = "拉伸模式", EditorType = PropertyEditorType.Enum, DefaultValue = "Uniform", Category = "外观",
                EnumOptions = new[] { "None|原始", "Fill|填充", "Uniform|等比", "UniformToFill|等比填充" }
            },
            new PropertyDescriptor { Key = "opacity", DisplayName = "不透明度", EditorType = PropertyEditorType.Number, DefaultValue = "1", Category = "外观" },

            // ============ B3.2: GraphicView WinCC 扩展（PDF GraphicView Picture/ErrorPicture）============
            new PropertyDescriptor { Key = "sourceErrorImage", DisplayName = "加载失败图", EditorType = PropertyEditorType.String, DefaultValue = "", Category = "数据",
                Description = "B3.2: source 加载失败时回退该图。WinCC ErrorPicture。" },
            new PropertyDescriptor { Key = "fitToObject", DisplayName = "适应控件大小", EditorType = PropertyEditorType.Boolean, DefaultValue = "true", Category = "外观",
                Description = "B3.2: false 时按图片原始尺寸渲染。WinCC FitToObject。" },
            new PropertyDescriptor { Key = "animationFps", DisplayName = "动画帧率(GIF)", EditorType = PropertyEditorType.Integer, DefaultValue = "0", Category = "外观",
                Description = "B3.2: GIF 帧率覆盖；0 = 使用 GIF 原始帧率。" },
            new PropertyDescriptor
            {
                Key = "horizontalAlignment", DisplayName = "水平对齐", EditorType = PropertyEditorType.Enum, DefaultValue = "Center", Category = "外观",
                EnumOptions = new[] { "Left|左", "Center|中", "Right|右", "Stretch|拉伸" }
            },
            new PropertyDescriptor
            {
                Key = "verticalAlignment", DisplayName = "垂直对齐", EditorType = PropertyEditorType.Enum, DefaultValue = "Center", Category = "外观",
                EnumOptions = new[] { "Top|上", "Center|中", "Bottom|下", "Stretch|拉伸" }
            },
        }
    };

    private static WidgetSchema BuildIoGraphic() => new()
    {
        TypeId = "io-graphic",
        Properties = new[]
        {
            new PropertyDescriptor { Key = "variable", DisplayName = "变量", EditorType = PropertyEditorType.TagAddress, DefaultValue = "", Category = "数据" },
            new PropertyDescriptor
            {
                Key = "mode", DisplayName = "模式", EditorType = PropertyEditorType.Enum, DefaultValue = "Output", Category = "数据",
                EnumOptions = new[] { "Input|输入", "Output|仅输出" },
                Description = "B1A: WinCC GraphicIOField Mode 只有 Input/Output 两值"
            },
            new PropertyDescriptor { Key = "entries", DisplayName = "图形条目", EditorType = PropertyEditorType.GraphicListRef, DefaultValue = "", Category = "数据", Description = "引用图形列表 {graphicList:xxx} 或 'value=path;...'" },
            new PropertyDescriptor
            {
                Key = "stretch", DisplayName = "拉伸", EditorType = PropertyEditorType.Enum, DefaultValue = "Uniform", Category = "外观",
                EnumOptions = new[] { "None|原始", "Fill|填充", "Uniform|等比", "UniformToFill|等比填充" }
            },

            // ============ B3.2: GraphicIOField WinCC 扩展（PDF GraphicIOField）============
            new PropertyDescriptor { Key = "defaultPicture", DisplayName = "默认图(无匹配)", EditorType = PropertyEditorType.String, DefaultValue = "", Category = "数据",
                Description = "B3.2: variable 值无对应 entries 项时显示。WinCC DefaultGraphic。" },
            new PropertyDescriptor { Key = "pictureOnError", DisplayName = "错误时图片", EditorType = PropertyEditorType.String, DefaultValue = "", Category = "数据",
                Description = "B3.2: 加载失败或变量未知时显示。WinCC ErrorGraphic。" },
            new PropertyDescriptor { Key = "fitToObject", DisplayName = "适应控件大小", EditorType = PropertyEditorType.Boolean, DefaultValue = "true", Category = "外观" },
            new PropertyDescriptor { Key = "showText", DisplayName = "下方显示值文本", EditorType = PropertyEditorType.Boolean, DefaultValue = "false", Category = "外观",
                Description = "B3.2: true 时在图形下方显示 variable 当前值。" },
        }
    };

    private static WidgetSchema BuildDateTime() => new()
    {
        TypeId = "datetime",
        Properties = new[]
        {
            new PropertyDescriptor
            {
                Key = "mode", DisplayName = "模式", EditorType = PropertyEditorType.Enum, DefaultValue = "SystemTime", Category = "数据",
                EnumOptions = new[] { "SystemTime|系统时间", "Tag|读 Tag", "Input|输入回写" }
            },
            new PropertyDescriptor { Key = "variable", DisplayName = "变量", EditorType = PropertyEditorType.TagAddress, DefaultValue = "", Category = "数据" },
            new PropertyDescriptor { Key = "format", DisplayName = "格式", EditorType = PropertyEditorType.String, DefaultValue = "yyyy-MM-dd HH:mm:ss", Category = "格式" },
            new PropertyDescriptor { Key = "background", DisplayName = "背景色", EditorType = PropertyEditorType.Color, DefaultValue = "#FFFFFF", Category = "外观" },
            new PropertyDescriptor { Key = "foreground", DisplayName = "前景色", EditorType = PropertyEditorType.Color, DefaultValue = "#0F172A", Category = "外观" },

            // ============ B3.2: DateTimeField WinCC 扩展（PDF DateTimeField）============
            new PropertyDescriptor
            {
                Key = "dataFormat", DisplayName = "数据格式", EditorType = PropertyEditorType.Enum, DefaultValue = "DateTime", Category = "格式",
                EnumOptions = new[] { "DateOnly|仅日期", "TimeOnly|仅时间", "DateTime|日期时间" },
                Description = "B3.2: WinCC DataFormat — 选择 DateOnly/TimeOnly 时自动用对应缩短格式。",
            },
            new PropertyDescriptor { Key = "showCalendarButton", DisplayName = "显示日历按钮", EditorType = PropertyEditorType.Boolean, DefaultValue = "true", Category = "外观",
                Description = "B3.2: 输入模式下显示日期选择器按钮。WinCC ShowCalendarButton。" },
            new PropertyDescriptor { Key = "showUpDownButton", DisplayName = "显示步进按钮", EditorType = PropertyEditorType.Boolean, DefaultValue = "false", Category = "外观",
                Description = "B3.2: 输入模式下显示 ▲▼ 调整年月日。WinCC ShowUpDown。" },
            new PropertyDescriptor { Key = "useLocalTime", DisplayName = "使用本地时间", EditorType = PropertyEditorType.Boolean, DefaultValue = "true", Category = "数据",
                Description = "B3.2: false = UTC。WinCC UseLocalTime。" },
        }
    };

    private static WidgetSchema BuildSlider() => new()
    {
        TypeId = "slider",
        Properties = new[]
        {
            new PropertyDescriptor { Key = "variable", DisplayName = "变量", EditorType = PropertyEditorType.TagAddress, DefaultValue = "", Category = "数据" },
            new PropertyDescriptor { Key = "minValue", DisplayName = "最小值", EditorType = PropertyEditorType.Number, DefaultValue = "0", Category = "限值" },
            new PropertyDescriptor { Key = "maxValue", DisplayName = "最大值", EditorType = PropertyEditorType.Number, DefaultValue = "100", Category = "限值" },
            new PropertyDescriptor { Key = "step", DisplayName = "步长", EditorType = PropertyEditorType.Number, DefaultValue = "1", Category = "限值" },
            new PropertyDescriptor
            {
                Key = "orientation", DisplayName = "方向", EditorType = PropertyEditorType.Enum, DefaultValue = "horizontal", Category = "布局",
                EnumOptions = new[] { "horizontal|水平", "vertical|垂直" }
            },
            new PropertyDescriptor { Key = "showLabel", DisplayName = "显示标签", EditorType = PropertyEditorType.Boolean, DefaultValue = "false", Category = "外观" },
            new PropertyDescriptor { Key = "showValue", DisplayName = "显示数值", EditorType = PropertyEditorType.Boolean, DefaultValue = "true", Category = "外观" },
            new PropertyDescriptor { Key = "snapToStep", DisplayName = "对齐步长", EditorType = PropertyEditorType.Boolean, DefaultValue = "true", Category = "行为" },
            new PropertyDescriptor { Key = "writeOnChange", DisplayName = "拖动写入", EditorType = PropertyEditorType.Boolean, DefaultValue = "false", Category = "行为", Description = "false=松手再写；true=拖动即写" },

            // ============ B3.2: Slider WinCC 扩展（PDF Slider Table）============
            new PropertyDescriptor { Key = "smallChange", DisplayName = "微调步长", EditorType = PropertyEditorType.Number, DefaultValue = "1", Category = "行为",
                Description = "B3.2: 方向键 / 单击轨道时的步长。WinCC SmallChange。" },
            new PropertyDescriptor { Key = "largeChange", DisplayName = "翻页步长", EditorType = PropertyEditorType.Number, DefaultValue = "10", Category = "行为",
                Description = "B3.2: PageUp/PageDown 步长。WinCC LargeChange。" },
            new PropertyDescriptor { Key = "showTicks", DisplayName = "显示刻度", EditorType = PropertyEditorType.Boolean, DefaultValue = "false", Category = "外观",
                Description = "B3.2: WinCC TickPlacement。" },
            new PropertyDescriptor { Key = "tickFrequency", DisplayName = "刻度间隔", EditorType = PropertyEditorType.Number, DefaultValue = "10", Category = "外观" },
            new PropertyDescriptor
            {
                Key = "tickPlacement", DisplayName = "刻度位置", EditorType = PropertyEditorType.Enum, DefaultValue = "BottomRight", Category = "外观",
                EnumOptions = new[] { "None|无", "TopLeft|上/左", "BottomRight|下/右", "Both|两侧" }
            },
            new PropertyDescriptor
            {
                Key = "direction", DisplayName = "方向反转", EditorType = PropertyEditorType.Enum, DefaultValue = "Normal", Category = "布局",
                EnumOptions = new[] { "Normal|正常", "Reversed|反向" },
                Description = "B3.2: Reversed = 大值在左/下。WinCC IsDirectionReversed。"
            },
        }
    };

    private static WidgetSchema BuildScrollbar() => new()
    {
        TypeId = "scrollbar",
        Properties = new[]
        {
            new PropertyDescriptor { Key = "variable", DisplayName = "变量", EditorType = PropertyEditorType.TagAddress, DefaultValue = "", Category = "数据" },
            new PropertyDescriptor { Key = "minValue", DisplayName = "最小值", EditorType = PropertyEditorType.Number, DefaultValue = "0", Category = "限值" },
            new PropertyDescriptor { Key = "maxValue", DisplayName = "最大值", EditorType = PropertyEditorType.Number, DefaultValue = "100", Category = "限值" },
            new PropertyDescriptor { Key = "step", DisplayName = "步长", EditorType = PropertyEditorType.Number, DefaultValue = "1", Category = "限值" },
            new PropertyDescriptor
            {
                Key = "orientation", DisplayName = "方向", EditorType = PropertyEditorType.Enum, DefaultValue = "horizontal", Category = "布局",
                EnumOptions = new[] { "horizontal|水平", "vertical|垂直" }
            },
            new PropertyDescriptor { Key = "showLabel", DisplayName = "显示标签", EditorType = PropertyEditorType.Boolean, DefaultValue = "false", Category = "外观" },
            new PropertyDescriptor { Key = "showValue", DisplayName = "显示数值", EditorType = PropertyEditorType.Boolean, DefaultValue = "false", Category = "外观" },
            new PropertyDescriptor { Key = "snapToStep", DisplayName = "对齐步长", EditorType = PropertyEditorType.Boolean, DefaultValue = "true", Category = "行为" },
            new PropertyDescriptor { Key = "writeOnChange", DisplayName = "拖动写入", EditorType = PropertyEditorType.Boolean, DefaultValue = "false", Category = "行为" },

            // ============ B3.2: ScrollBar WinCC 扩展（PDF ScrollBar Table）============
            new PropertyDescriptor { Key = "smallChange", DisplayName = "微调步长", EditorType = PropertyEditorType.Number, DefaultValue = "1", Category = "行为" },
            new PropertyDescriptor { Key = "largeChange", DisplayName = "翻页步长", EditorType = PropertyEditorType.Number, DefaultValue = "10", Category = "行为" },
            new PropertyDescriptor { Key = "showButtons", DisplayName = "显示端按钮", EditorType = PropertyEditorType.Boolean, DefaultValue = "true", Category = "外观",
                Description = "B3.2: 两端的 ▲▼ 步进按钮。WinCC ShowLineButtons。" },
            new PropertyDescriptor
            {
                Key = "direction", DisplayName = "方向反转", EditorType = PropertyEditorType.Enum, DefaultValue = "Normal", Category = "布局",
                EnumOptions = new[] { "Normal|正常", "Reversed|反向" }
            },
            new PropertyDescriptor { Key = "thumbSize", DisplayName = "滑块大小(viewport)", EditorType = PropertyEditorType.Number, DefaultValue = "0", Category = "外观",
                Description = "B3.2: WPF ViewportSize；0 = 默认。" },
        }
    };

    private static WidgetSchema BuildClock() => new()
    {
        TypeId = "clock",
        Properties = new[]
        {
            new PropertyDescriptor
            {
                Key = "mode", DisplayName = "样式", EditorType = PropertyEditorType.Enum, DefaultValue = "digital", Category = "数据",
                EnumOptions = new[] { "digital|数字", "analog|指针" }
            },
            new PropertyDescriptor { Key = "format", DisplayName = "数字格式", EditorType = PropertyEditorType.String, DefaultValue = "yyyy-MM-dd HH:mm:ss", Category = "格式" },
            new PropertyDescriptor { Key = "foreground", DisplayName = "前景色", EditorType = PropertyEditorType.Color, DefaultValue = "#0F172A", Category = "外观" },
            new PropertyDescriptor { Key = "background", DisplayName = "背景色", EditorType = PropertyEditorType.Color, DefaultValue = "#FFFFFF", Category = "外观" },
            new PropertyDescriptor { Key = "fontSize", DisplayName = "字号", EditorType = PropertyEditorType.Number, DefaultValue = "14", Category = "外观" },
            new PropertyDescriptor { Key = "analogShowSeconds", DisplayName = "指针显示秒针", EditorType = PropertyEditorType.Boolean, DefaultValue = "true", Category = "外观" },

            // ============ B3.2: Clock WinCC 扩展（PDF Clock 章节）============
            new PropertyDescriptor { Key = "showSeconds", DisplayName = "数字显示秒", EditorType = PropertyEditorType.Boolean, DefaultValue = "true", Category = "外观",
                Description = "B3.2: false 时数字模式不显示秒（自动剥离 'ss' 格式）。WinCC ShowSeconds。" },
            new PropertyDescriptor { Key = "timeZone", DisplayName = "时区", EditorType = PropertyEditorType.String, DefaultValue = "", Category = "数据",
                Description = "B3.2: TimeZoneInfo Id（如 'China Standard Time'）。留空 = 本地时区。WinCC TimeZone。" },
            new PropertyDescriptor
            {
                Key = "synchronizationMode", DisplayName = "同步模式", EditorType = PropertyEditorType.Enum, DefaultValue = "Local", Category = "数据",
                EnumOptions = new[] { "Local|本地", "Server|服务器", "NTP|NTP" },
                Description = "B3.2: 时间源。WinCC SynchronizationMode。",
            },
            new PropertyDescriptor { Key = "use24Hour", DisplayName = "24 小时制", EditorType = PropertyEditorType.Boolean, DefaultValue = "true", Category = "格式" },
            new PropertyDescriptor { Key = "showDate", DisplayName = "数字显示日期", EditorType = PropertyEditorType.Boolean, DefaultValue = "true", Category = "外观" },
        }
    };

    private static WidgetSchema BuildCombobox() => new()
    {
        TypeId = "combobox",
        Properties = new[]
        {
            new PropertyDescriptor { Key = "variable", DisplayName = "变量", EditorType = PropertyEditorType.TagAddress, DefaultValue = "", Category = "数据" },
            new PropertyDescriptor { Key = "items", DisplayName = "选项", EditorType = PropertyEditorType.TextListRef, DefaultValue = "0=选项1;1=选项2;2=选项3", Category = "数据", Description = "'value=text;...' 或 {textList:xxx}" },
        }
    };

    private static WidgetSchema BuildListbox() => new()
    {
        TypeId = "listbox",
        Properties = new[]
        {
            new PropertyDescriptor { Key = "variable", DisplayName = "变量", EditorType = PropertyEditorType.TagAddress, DefaultValue = "", Category = "数据" },
            new PropertyDescriptor { Key = "items", DisplayName = "选项", EditorType = PropertyEditorType.TextListRef, DefaultValue = "0=选项1;1=选项2;2=选项3", Category = "数据" },
        }
    };

    private static WidgetSchema BuildCheckbox() => new()
    {
        TypeId = "checkbox",
        Properties = new[]
        {
            new PropertyDescriptor { Key = "variable", DisplayName = "变量", EditorType = PropertyEditorType.TagAddress, DefaultValue = "", Category = "数据" },
            new PropertyDescriptor { Key = "text", DisplayName = "文本", EditorType = PropertyEditorType.String, DefaultValue = "选项", Category = "文本" },
            new PropertyDescriptor { Key = "checkedColor", DisplayName = "选中颜色", EditorType = PropertyEditorType.Color, DefaultValue = "#10B981", Category = "外观" },
            new PropertyDescriptor { Key = "uncheckedColor", DisplayName = "未选颜色", EditorType = PropertyEditorType.Color, DefaultValue = "#94A3B8", Category = "外观" },
            new PropertyDescriptor { Key = "foreground", DisplayName = "前景色", EditorType = PropertyEditorType.Color, DefaultValue = "#0F172A", Category = "外观" },

            // ============ B3.2: CheckBox WinCC 扩展（PDF CheckBox 章节）============
            new PropertyDescriptor { Key = "checkedText", DisplayName = "选中文本", EditorType = PropertyEditorType.String, DefaultValue = "", Category = "文本",
                Description = "B3.2: 选中状态下覆盖 text。留空 = 沿用 text。WinCC CheckedText。" },
            new PropertyDescriptor { Key = "uncheckedText", DisplayName = "未选文本", EditorType = PropertyEditorType.String, DefaultValue = "", Category = "文本",
                Description = "B3.2: 未选状态下覆盖 text。" },
            new PropertyDescriptor { Key = "threeState", DisplayName = "三态模式", EditorType = PropertyEditorType.Boolean, DefaultValue = "false", Category = "行为",
                Description = "B3.2: 启用时支持 Null/Indeterminate 中间态。WinCC ThreeState。" },
            new PropertyDescriptor { Key = "indeterminateText", DisplayName = "中间态文本", EditorType = PropertyEditorType.String, DefaultValue = "未定", Category = "文本",
                Description = "B3.2: threeState=true 且值为 null/-1 时显示。" },
            new PropertyDescriptor { Key = "indeterminateColor", DisplayName = "中间态颜色", EditorType = PropertyEditorType.Color, DefaultValue = "#FBBF24", Category = "外观" },
        }
    };

    private static WidgetSchema BuildOptionGroup() => new()
    {
        TypeId = "optiongroup",
        Properties = new[]
        {
            new PropertyDescriptor { Key = "variable", DisplayName = "变量", EditorType = PropertyEditorType.TagAddress, DefaultValue = "", Category = "数据" },
            new PropertyDescriptor { Key = "items", DisplayName = "选项", EditorType = PropertyEditorType.TextListRef, DefaultValue = "0=选项A;1=选项B;2=选项C", Category = "数据" },
            new PropertyDescriptor
            {
                Key = "orientation", DisplayName = "方向", EditorType = PropertyEditorType.Enum, DefaultValue = "vertical", Category = "布局",
                EnumOptions = new[] { "horizontal|水平", "vertical|垂直" }
            },
        }
    };

    private static WidgetSchema BuildRoundButton() => new()
    {
        TypeId = "round-button",
        Properties = new[]
        {
            new PropertyDescriptor { Key = "text", DisplayName = "按钮文本", EditorType = PropertyEditorType.String, DefaultValue = "按钮", Category = "文本" },
            new PropertyDescriptor { Key = "fontSize", DisplayName = "字号", EditorType = PropertyEditorType.Number, DefaultValue = "14", Category = "文本" },
            new PropertyDescriptor { Key = "foreground", DisplayName = "文字颜色", EditorType = PropertyEditorType.Color, DefaultValue = "#FFFFFF", Category = "外观" },
            new PropertyDescriptor { Key = "background", DisplayName = "背景色", EditorType = PropertyEditorType.Color, DefaultValue = "#2563EB", Category = "外观" },
            new PropertyDescriptor { Key = "pressedBackground", DisplayName = "按下背景色", EditorType = PropertyEditorType.Color, DefaultValue = "#1E40AF", Category = "外观" },
        }
    };

    private static WidgetSchema BuildAlarmView() => new()
    {
        TypeId = "alarm-view",
        Properties = new[]
        {
            new PropertyDescriptor
            {
                Key = "filterCategory", DisplayName = "分类过滤", EditorType = PropertyEditorType.Enum, DefaultValue = "All", Category = "数据",
                EnumOptions = new[] { "All|全部", "Info|信息", "Warning|警告", "Error|错误", "Alarm|报警" }
            },
            new PropertyDescriptor { Key = "columns", DisplayName = "列配置", EditorType = PropertyEditorType.Json, DefaultValue = "[]", Category = "数据", Description = "JSON 数组，每项 {field,header,width}" },
            new PropertyDescriptor { Key = "maxRows", DisplayName = "最大行数", EditorType = PropertyEditorType.Integer, DefaultValue = "100", Category = "数据" },
            new PropertyDescriptor { Key = "showAck", DisplayName = "显示确认按钮", EditorType = PropertyEditorType.Boolean, DefaultValue = "true", Category = "外观" },
            new PropertyDescriptor { Key = "background", DisplayName = "背景色", EditorType = PropertyEditorType.Color, DefaultValue = "#FFFFFF", Category = "外观" },

            // ============ B2E: WinCC AlarmControl 扩展（PDF Table 1-1 / 1-3）============
            // 8 个状态色（来 IN / 来确认 INA / 已确认 CA / 走未确认 LIA / 走已确认 LCA / 闪烁 / Hover / Selected）
            new PropertyDescriptor { Key = "colorIn", DisplayName = "来报警色 (IN)", EditorType = PropertyEditorType.Color, DefaultValue = "#DC2626", Category = "状态色" },
            new PropertyDescriptor { Key = "colorInAck", DisplayName = "来已确认色 (INA)", EditorType = PropertyEditorType.Color, DefaultValue = "#F59E0B", Category = "状态色" },
            new PropertyDescriptor { Key = "colorCame", DisplayName = "已确认色 (CA)", EditorType = PropertyEditorType.Color, DefaultValue = "#22C55E", Category = "状态色" },
            new PropertyDescriptor { Key = "colorLeftInactive", DisplayName = "走未确认色 (LIA)", EditorType = PropertyEditorType.Color, DefaultValue = "#94A3B8", Category = "状态色" },
            new PropertyDescriptor { Key = "colorLeftConfirmed", DisplayName = "走已确认色 (LCA)", EditorType = PropertyEditorType.Color, DefaultValue = "#64748B", Category = "状态色" },
            new PropertyDescriptor { Key = "colorCleared", DisplayName = "已清除色 (CL)", EditorType = PropertyEditorType.Color, DefaultValue = "#CBD5E1", Category = "状态色",
                Description = "B3.1: 操作员手动清除后的行色（WinCC AlarmControl Cleared 状态）。" },
            new PropertyDescriptor { Key = "colorFlashing", DisplayName = "闪烁色", EditorType = PropertyEditorType.Color, DefaultValue = "#FBBF24", Category = "状态色" },
            new PropertyDescriptor { Key = "colorHover", DisplayName = "悬停色", EditorType = PropertyEditorType.Color, DefaultValue = "#E0F2FE", Category = "状态色" },
            new PropertyDescriptor { Key = "colorSelected", DisplayName = "选中色", EditorType = PropertyEditorType.Color, DefaultValue = "#BAE6FD", Category = "状态色" },

            // 过滤 / 时间窗
            new PropertyDescriptor { Key = "alarmClassFilter", DisplayName = "报警类过滤", EditorType = PropertyEditorType.String, DefaultValue = "", Category = "过滤",
                Description = "多类用逗号分隔，例 '系统,工艺,过程'。留空 = 全部。" },
            new PropertyDescriptor { Key = "timeSpan", DisplayName = "时间窗", EditorType = PropertyEditorType.Enum, DefaultValue = "1h", Category = "过滤",
                EnumOptions = new[] { "1h|1 小时", "4h|4 小时", "8h|8 小时", "24h|24 小时", "History|历史" } },

            // 排序
            new PropertyDescriptor { Key = "sortColumn", DisplayName = "排序列", EditorType = PropertyEditorType.String, DefaultValue = "Time", Category = "排序" },
            new PropertyDescriptor { Key = "sortDirection", DisplayName = "排序方向", EditorType = PropertyEditorType.Enum, DefaultValue = "Descending", Category = "排序",
                EnumOptions = new[] { "Ascending|升序", "Descending|降序" } },

            // 导出
            new PropertyDescriptor { Key = "exportFormat", DisplayName = "导出格式", EditorType = PropertyEditorType.Enum, DefaultValue = "CSV", Category = "导出",
                EnumOptions = new[] { "CSV|CSV", "Excel|Excel", "PDF|PDF" } },
        }
    };

    private static WidgetSchema BuildTableView() => new()
    {
        TypeId = "table-view",
        Properties = new[]
        {
            new PropertyDescriptor { Key = "columns", DisplayName = "列配置", EditorType = PropertyEditorType.Json, DefaultValue = "[]", Category = "数据", Description = "JSON 数组，每项 {field,header,width}" },
            new PropertyDescriptor { Key = "dataSource", DisplayName = "数据源", EditorType = PropertyEditorType.String, DefaultValue = "", Category = "数据", Description = "Tag 表前缀或数据集 Id" },
            new PropertyDescriptor { Key = "showHeader", DisplayName = "显示表头", EditorType = PropertyEditorType.Boolean, DefaultValue = "true", Category = "外观" },
            new PropertyDescriptor { Key = "alternateRowColor", DisplayName = "斑马纹颜色", EditorType = PropertyEditorType.Color, DefaultValue = "#F8FAFC", Category = "外观" },
            new PropertyDescriptor { Key = "background", DisplayName = "背景色", EditorType = PropertyEditorType.Color, DefaultValue = "#FFFFFF", Category = "外观" },

            // ============ B3.3: TableView 通用 HMI 控件扩展（参考 WinCC TableControl/AlarmControl）============
            new PropertyDescriptor { Key = "rowHeight", DisplayName = "行高", EditorType = PropertyEditorType.Integer, DefaultValue = "22", Category = "外观" },
            new PropertyDescriptor { Key = "allowSort", DisplayName = "允许排序", EditorType = PropertyEditorType.Boolean, DefaultValue = "true", Category = "行为" },
            new PropertyDescriptor { Key = "allowFilter", DisplayName = "允许列过滤", EditorType = PropertyEditorType.Boolean, DefaultValue = "false", Category = "行为" },
            new PropertyDescriptor { Key = "showRowNumbers", DisplayName = "显示行号", EditorType = PropertyEditorType.Boolean, DefaultValue = "false", Category = "外观" },
            new PropertyDescriptor
            {
                Key = "exportFormat", DisplayName = "导出格式", EditorType = PropertyEditorType.Enum, DefaultValue = "CSV", Category = "导出",
                EnumOptions = new[] { "CSV|CSV", "Excel|Excel", "JSON|JSON" }
            },
            new PropertyDescriptor { Key = "selectionMode", DisplayName = "选择模式", EditorType = PropertyEditorType.Enum, DefaultValue = "Single", Category = "行为",
                EnumOptions = new[] { "Single|单选", "Extended|扩展", "None|禁选" } },
            new PropertyDescriptor { Key = "refreshInterval", DisplayName = "刷新间隔(秒)", EditorType = PropertyEditorType.Number, DefaultValue = "0", Category = "行为",
                Description = "B3.3: >0 时按周期重读 dataSource；0 = 仅由 Tag 触发。" },
        }
    };

    private static WidgetSchema BuildScreenWindow() => new()
    {
        TypeId = "screen-window",
        Properties = new[]
        {
            new PropertyDescriptor { Key = "pageRoute", DisplayName = "嵌入页面", EditorType = PropertyEditorType.PageRoute, DefaultValue = "", Category = "数据" },
            new PropertyDescriptor { Key = "showHeader", DisplayName = "显示标题栏", EditorType = PropertyEditorType.Boolean, DefaultValue = "true", Category = "外观" },
            new PropertyDescriptor { Key = "zoomToFit", DisplayName = "缩放适应", EditorType = PropertyEditorType.Boolean, DefaultValue = "true", Category = "外观" },
            new PropertyDescriptor { Key = "background", DisplayName = "背景色", EditorType = PropertyEditorType.Color, DefaultValue = "#FFFFFF", Category = "外观" },

            // ============ B3.3: ScreenWindow 扩展（参考 WinCC PictureWindow）============
            new PropertyDescriptor { Key = "showTitle", DisplayName = "显示标题", EditorType = PropertyEditorType.Boolean, DefaultValue = "true", Category = "外观" },
            new PropertyDescriptor { Key = "showCloseButton", DisplayName = "显示关闭按钮", EditorType = PropertyEditorType.Boolean, DefaultValue = "false", Category = "外观",
                Description = "B3.3: WinCC ShowCloseButton。" },
            new PropertyDescriptor { Key = "modal", DisplayName = "模态对话框", EditorType = PropertyEditorType.Boolean, DefaultValue = "false", Category = "行为" },
            new PropertyDescriptor { Key = "autoSize", DisplayName = "自适应大小", EditorType = PropertyEditorType.Boolean, DefaultValue = "false", Category = "外观",
                Description = "B3.3: true 时按嵌入页面原始尺寸；false 时按外部矩形。" },
            new PropertyDescriptor { Key = "scrollEnabled", DisplayName = "允许滚动", EditorType = PropertyEditorType.Boolean, DefaultValue = "false", Category = "行为" },
            new PropertyDescriptor { Key = "titleText", DisplayName = "标题文本", EditorType = PropertyEditorType.String, DefaultValue = "", Category = "文本",
                Description = "B3.3: 留空时使用 pageRoute 的页面名。" },
        }
    };

    // ---------------- html-browser (P9A) ----------------
    private static WidgetSchema BuildHtmlBrowser() => new()
    {
        TypeId = "html-browser",
        Properties = new[]
        {
            new PropertyDescriptor { Key = "url", DisplayName = "URL", EditorType = PropertyEditorType.String, DefaultValue = "https://www.bing.com", Category = "数据" },
            new PropertyDescriptor { Key = "navigateOnLoad", DisplayName = "加载时跳转", EditorType = PropertyEditorType.Boolean, DefaultValue = "true", Category = "行为" },
            new PropertyDescriptor { Key = "showToolbar", DisplayName = "显示工具栏", EditorType = PropertyEditorType.Boolean, DefaultValue = "true", Category = "外观" },
            new PropertyDescriptor { Key = "background", DisplayName = "背景色", EditorType = PropertyEditorType.Color, DefaultValue = "#FFFFFF", Category = "外观" },

            // ============ B3.3: HtmlBrowser 扩展（参考 WinCC HMIRuntime browser host）============
            new PropertyDescriptor { Key = "allowNavigation", DisplayName = "允许导航", EditorType = PropertyEditorType.Boolean, DefaultValue = "true", Category = "行为",
                Description = "B3.3: false 时锁定到初始 URL，禁止跳转。" },
            new PropertyDescriptor { Key = "showAddressBar", DisplayName = "显示地址栏", EditorType = PropertyEditorType.Boolean, DefaultValue = "false", Category = "外观" },
            new PropertyDescriptor { Key = "showProgressBar", DisplayName = "显示进度条", EditorType = PropertyEditorType.Boolean, DefaultValue = "true", Category = "外观" },
            new PropertyDescriptor { Key = "homeUrl", DisplayName = "主页 URL", EditorType = PropertyEditorType.String, DefaultValue = "", Category = "数据" },
            new PropertyDescriptor { Key = "userAgent", DisplayName = "User-Agent", EditorType = PropertyEditorType.String, DefaultValue = "", Category = "数据",
                Description = "B3.3: 留空时使用 WebView2 默认。" },
            new PropertyDescriptor { Key = "javascriptEnabled", DisplayName = "启用 JavaScript", EditorType = PropertyEditorType.Boolean, DefaultValue = "true", Category = "行为" },
        }
    };

    // ---------------- pdf-view (P9B) ----------------
    private static WidgetSchema BuildPdfView() => new()
    {
        TypeId = "pdf-view",
        Properties = new[]
        {
            new PropertyDescriptor { Key = "filePath", DisplayName = "文件路径", EditorType = PropertyEditorType.String, DefaultValue = "", Category = "数据", Description = "本地 PDF 路径或 http(s):// URL" },
            new PropertyDescriptor { Key = "fitToWidth", DisplayName = "适应宽度", EditorType = PropertyEditorType.Boolean, DefaultValue = "true", Category = "外观" },
            new PropertyDescriptor { Key = "background", DisplayName = "背景色", EditorType = PropertyEditorType.Color, DefaultValue = "#F8FAFC", Category = "外观" },

            // ============ B3.3: PdfView 扩展 ============
            new PropertyDescriptor
            {
                Key = "zoomMode", DisplayName = "缩放模式", EditorType = PropertyEditorType.Enum, DefaultValue = "FitWidth", Category = "外观",
                EnumOptions = new[] { "FitWidth|适应宽度", "FitPage|适应页面", "Actual|实际大小", "Custom|自定义" }
            },
            new PropertyDescriptor { Key = "zoomLevel", DisplayName = "缩放比例", EditorType = PropertyEditorType.Number, DefaultValue = "1.0", Category = "外观",
                Description = "B3.3: zoomMode=Custom 时生效。" },
            new PropertyDescriptor { Key = "pageNumber", DisplayName = "起始页码", EditorType = PropertyEditorType.Integer, DefaultValue = "1", Category = "数据" },
            new PropertyDescriptor { Key = "showThumbnails", DisplayName = "显示缩略图侧栏", EditorType = PropertyEditorType.Boolean, DefaultValue = "false", Category = "外观" },
            new PropertyDescriptor { Key = "showToolbar", DisplayName = "显示工具栏", EditorType = PropertyEditorType.Boolean, DefaultValue = "true", Category = "外观" },
            new PropertyDescriptor { Key = "printEnabled", DisplayName = "允许打印", EditorType = PropertyEditorType.Boolean, DefaultValue = "true", Category = "行为" },
            new PropertyDescriptor { Key = "downloadEnabled", DisplayName = "允许下载", EditorType = PropertyEditorType.Boolean, DefaultValue = "true", Category = "行为" },
        }
    };

    // ---------------- media-player (P9C) ----------------
    private static WidgetSchema BuildMediaPlayer() => new()
    {
        TypeId = "media-player",
        Properties = new[]
        {
            new PropertyDescriptor { Key = "source", DisplayName = "媒体源", EditorType = PropertyEditorType.String, DefaultValue = "", Category = "数据", Description = "本地 mp4/mp3 路径或 http(s):// URL" },
            new PropertyDescriptor { Key = "autoPlay", DisplayName = "自动播放", EditorType = PropertyEditorType.Boolean, DefaultValue = "false", Category = "行为" },
            new PropertyDescriptor { Key = "loop", DisplayName = "循环", EditorType = PropertyEditorType.Boolean, DefaultValue = "false", Category = "行为" },
            new PropertyDescriptor { Key = "showToolbar", DisplayName = "显示工具栏", EditorType = PropertyEditorType.Boolean, DefaultValue = "true", Category = "外观" },
            new PropertyDescriptor { Key = "volume", DisplayName = "音量", EditorType = PropertyEditorType.Number, DefaultValue = "0.5", Category = "行为", Description = "0~1" },

            // ============ B3.3: MediaPlayer 扩展（参考通用 HMI 视频墙控件）============
            new PropertyDescriptor { Key = "showControls", DisplayName = "显示控制条", EditorType = PropertyEditorType.Boolean, DefaultValue = "true", Category = "外观" },
            new PropertyDescriptor { Key = "muted", DisplayName = "静音", EditorType = PropertyEditorType.Boolean, DefaultValue = "false", Category = "行为" },
            new PropertyDescriptor { Key = "playbackRate", DisplayName = "播放倍速", EditorType = PropertyEditorType.Number, DefaultValue = "1.0", Category = "行为" },
            new PropertyDescriptor
            {
                Key = "stretch", DisplayName = "画面拉伸", EditorType = PropertyEditorType.Enum, DefaultValue = "Uniform", Category = "外观",
                EnumOptions = new[] { "None|原始", "Fill|填充", "Uniform|等比", "UniformToFill|等比填充" }
            },
            new PropertyDescriptor { Key = "posterImage", DisplayName = "封面图", EditorType = PropertyEditorType.String, DefaultValue = "", Category = "外观",
                Description = "B3.3: 未播放时显示的静态图。" },
            new PropertyDescriptor { Key = "startPosition", DisplayName = "起始位置(秒)", EditorType = PropertyEditorType.Number, DefaultValue = "0", Category = "行为" },
        }
    };

    // ---------------- xy-trend (P9E) ----------------
    private static WidgetSchema BuildXyTrend() => new()
    {
        TypeId = "xy-trend",
        Properties = new[]
        {
            new PropertyDescriptor { Key = "xVariable", DisplayName = "X 变量", EditorType = PropertyEditorType.TagAddress, DefaultValue = "", Category = "数据" },
            new PropertyDescriptor { Key = "yVariable", DisplayName = "Y 变量", EditorType = PropertyEditorType.TagAddress, DefaultValue = "", Category = "数据" },
            new PropertyDescriptor
            {
                Key = "mode", DisplayName = "显示模式", EditorType = PropertyEditorType.Enum, DefaultValue = "Scatter", Category = "外观",
                EnumOptions = new[] { "Scatter|散点", "Line|连线", "Both|散点+连线" }
            },
            new PropertyDescriptor { Key = "xLabel", DisplayName = "X 轴标题", EditorType = PropertyEditorType.String, DefaultValue = "X", Category = "外观" },
            new PropertyDescriptor { Key = "yLabel", DisplayName = "Y 轴标题", EditorType = PropertyEditorType.String, DefaultValue = "Y", Category = "外观" },
            new PropertyDescriptor { Key = "xMin", DisplayName = "X 最小值", EditorType = PropertyEditorType.Number, DefaultValue = "auto", Category = "限值" },
            new PropertyDescriptor { Key = "xMax", DisplayName = "X 最大值", EditorType = PropertyEditorType.Number, DefaultValue = "auto", Category = "限值" },
            new PropertyDescriptor { Key = "yMin", DisplayName = "Y 最小值", EditorType = PropertyEditorType.Number, DefaultValue = "auto", Category = "限值" },
            new PropertyDescriptor { Key = "yMax", DisplayName = "Y 最大值", EditorType = PropertyEditorType.Number, DefaultValue = "auto", Category = "限值" },
            new PropertyDescriptor { Key = "color", DisplayName = "颜色", EditorType = PropertyEditorType.Color, DefaultValue = "#2563EB", Category = "外观" },
            new PropertyDescriptor { Key = "maxPoints", DisplayName = "最大点数", EditorType = PropertyEditorType.Integer, DefaultValue = "200", Category = "数据" },

            // ============ B3.3: XYTrend 扩展（参考 WinCC FunctionTrendControl）============
            new PropertyDescriptor { Key = "showGrid", DisplayName = "显示网格", EditorType = PropertyEditorType.Boolean, DefaultValue = "true", Category = "外观" },
            new PropertyDescriptor { Key = "showLegend", DisplayName = "显示图例", EditorType = PropertyEditorType.Boolean, DefaultValue = "true", Category = "外观" },
            new PropertyDescriptor { Key = "showRuler", DisplayName = "显示游标", EditorType = PropertyEditorType.Boolean, DefaultValue = "false", Category = "外观",
                Description = "B3.3: 鼠标 hover 显示 (x,y) 值。WinCC ShowRuler。" },
            new PropertyDescriptor { Key = "pointSize", DisplayName = "散点大小", EditorType = PropertyEditorType.Number, DefaultValue = "4", Category = "外观" },
            new PropertyDescriptor { Key = "lineThickness", DisplayName = "连线粗细", EditorType = PropertyEditorType.Number, DefaultValue = "1.5", Category = "外观" },
            new PropertyDescriptor { Key = "trailLength", DisplayName = "拖尾长度", EditorType = PropertyEditorType.Integer, DefaultValue = "0", Category = "行为",
                Description = "B3.3: >0 = 只显示最近 N 点（轨迹动画）。0 = 全部。" },
        }
    };

    // ---------------- report-view (P9F) ----------------
    private static WidgetSchema BuildReportView() => new()
    {
        TypeId = "report-view",
        Properties = new[]
        {
            new PropertyDescriptor { Key = "templateId", DisplayName = "报表模板", EditorType = PropertyEditorType.String, DefaultValue = "", Category = "数据", Description = "ProjectDocument.Reports 中的 ReportTemplate.Id" },
            new PropertyDescriptor { Key = "autoRefresh", DisplayName = "自动刷新", EditorType = PropertyEditorType.Boolean, DefaultValue = "false", Category = "行为" },
            new PropertyDescriptor { Key = "refreshInterval", DisplayName = "刷新间隔(秒)", EditorType = PropertyEditorType.Number, DefaultValue = "10", Category = "行为" },

            // ============ B3.3: ReportView 扩展（参考 WinCC ReportSystem）============
            new PropertyDescriptor { Key = "showToolbar", DisplayName = "显示工具栏", EditorType = PropertyEditorType.Boolean, DefaultValue = "true", Category = "外观" },
            new PropertyDescriptor { Key = "allowExport", DisplayName = "允许导出", EditorType = PropertyEditorType.Boolean, DefaultValue = "true", Category = "行为" },
            new PropertyDescriptor
            {
                Key = "exportFormat", DisplayName = "导出格式", EditorType = PropertyEditorType.Enum, DefaultValue = "PDF", Category = "导出",
                EnumOptions = new[] { "PDF|PDF", "Excel|Excel", "CSV|CSV", "HTML|HTML" }
            },
            new PropertyDescriptor { Key = "allowPrint", DisplayName = "允许打印", EditorType = PropertyEditorType.Boolean, DefaultValue = "true", Category = "行为" },
            new PropertyDescriptor { Key = "showParameterPanel", DisplayName = "显示参数面板", EditorType = PropertyEditorType.Boolean, DefaultValue = "false", Category = "外观",
                Description = "B3.3: 模板含参数时是否显示输入面板。" },
            new PropertyDescriptor
            {
                Key = "pageOrientation", DisplayName = "页面方向", EditorType = PropertyEditorType.Enum, DefaultValue = "Portrait", Category = "外观",
                EnumOptions = new[] { "Portrait|纵向", "Landscape|横向" }
            },
        }
    };

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

            // ============ B3.3: StatusForce 扩展（参考 WinCC OnlineTableControl + Force Value）============
            new PropertyDescriptor { Key = "showQuality", DisplayName = "显示质量码", EditorType = PropertyEditorType.Boolean, DefaultValue = "true", Category = "外观",
                Description = "B3.3: 显示 OPC UA StatusCode 列。" },
            new PropertyDescriptor { Key = "showTimestamp", DisplayName = "显示时间戳", EditorType = PropertyEditorType.Boolean, DefaultValue = "true", Category = "外观" },
            new PropertyDescriptor { Key = "allowForce", DisplayName = "允许强制值", EditorType = PropertyEditorType.Boolean, DefaultValue = "false", Category = "行为",
                Description = "B3.3: false 时即使 readonly=false 也不显示强制按钮。安全考虑默认关闭。" },
            new PropertyDescriptor { Key = "refreshInterval", DisplayName = "刷新间隔(秒)", EditorType = PropertyEditorType.Number, DefaultValue = "1", Category = "行为" },
            new PropertyDescriptor { Key = "highlightChanges", DisplayName = "高亮变化值", EditorType = PropertyEditorType.Boolean, DefaultValue = "true", Category = "外观",
                Description = "B3.3: 值变化时短暂高亮该行。" },
            new PropertyDescriptor { Key = "forceColor", DisplayName = "强制行颜色", EditorType = PropertyEditorType.Color, DefaultValue = "#FBBF24", Category = "外观" },
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

            // ============ B3.3: AlarmIndicator 扩展（参考 WinCC AlarmIndicator）============
            new PropertyDescriptor { Key = "filterPattern", DisplayName = "过滤模式", EditorType = PropertyEditorType.String, DefaultValue = "", Category = "数据",
                Description = "B3.3: Source/Message 包含此关键字。留空 = 不过滤。WinCC AlarmFilter。" },
            new PropertyDescriptor { Key = "alarmClassFilter", DisplayName = "报警类过滤", EditorType = PropertyEditorType.String, DefaultValue = "", Category = "数据",
                Description = "B3.3: 多类用逗号分隔。" },
            new PropertyDescriptor { Key = "autoNavigateTo", DisplayName = "新报警自动跳转", EditorType = PropertyEditorType.Boolean, DefaultValue = "false", Category = "行为" },
            new PropertyDescriptor
            {
                Key = "displayMode", DisplayName = "显示模式", EditorType = PropertyEditorType.Enum, DefaultValue = "Count", Category = "外观",
                EnumOptions = new[] { "Count|数字", "Dot|圆点", "Both|两者" }
            },
            new PropertyDescriptor { Key = "showOnlyActive", DisplayName = "仅活动报警", EditorType = PropertyEditorType.Boolean, DefaultValue = "true", Category = "数据" },
            new PropertyDescriptor { Key = "maxCountDisplay", DisplayName = "最大显示数", EditorType = PropertyEditorType.Integer, DefaultValue = "99", Category = "外观",
                Description = "B3.3: 超过显示 'N+'。" },
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

            // ============ B3.3: DiagnosticView 扩展（参考 WinCC SystemDiagnosticControl）============
            new PropertyDescriptor
            {
                Key = "showLevel", DisplayName = "显示等级", EditorType = PropertyEditorType.Enum, DefaultValue = "Info", Category = "数据",
                EnumOptions = new[] { "Error|仅错误", "Warning|警告以上", "Info|信息以上", "Verbose|全部" }
            },
            new PropertyDescriptor { Key = "autoRefresh", DisplayName = "自动刷新", EditorType = PropertyEditorType.Boolean, DefaultValue = "true", Category = "行为" },
            new PropertyDescriptor { Key = "showCpuUsage", DisplayName = "显示 CPU 占用", EditorType = PropertyEditorType.Boolean, DefaultValue = "false", Category = "外观" },
            new PropertyDescriptor { Key = "showMemoryUsage", DisplayName = "显示内存占用", EditorType = PropertyEditorType.Boolean, DefaultValue = "false", Category = "外观" },
            new PropertyDescriptor { Key = "showNetworkStats", DisplayName = "显示网络统计", EditorType = PropertyEditorType.Boolean, DefaultValue = "false", Category = "外观" },
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

            // ============ B3.3: UserView 扩展（参考 WinCC UserAdminControl）============
            new PropertyDescriptor { Key = "allowGroupChange", DisplayName = "允许修改用户组", EditorType = PropertyEditorType.Boolean, DefaultValue = "false", Category = "行为" },
            new PropertyDescriptor { Key = "passwordPolicyEnforced", DisplayName = "强制密码策略", EditorType = PropertyEditorType.Boolean, DefaultValue = "true", Category = "行为",
                Description = "B3.3: 密码长度 / 复杂度 / 失效期检查。" },
            new PropertyDescriptor { Key = "showLogonAttempts", DisplayName = "显示登录尝试", EditorType = PropertyEditorType.Boolean, DefaultValue = "false", Category = "外观" },
            new PropertyDescriptor { Key = "allowSelfPasswordChange", DisplayName = "允许自助改密", EditorType = PropertyEditorType.Boolean, DefaultValue = "true", Category = "行为" },
            new PropertyDescriptor { Key = "passwordMinLength", DisplayName = "密码最小长度", EditorType = PropertyEditorType.Integer, DefaultValue = "8", Category = "行为" },
            new PropertyDescriptor { Key = "passwordExpiryDays", DisplayName = "密码有效期(天)", EditorType = PropertyEditorType.Integer, DefaultValue = "90", Category = "行为",
                Description = "B3.3: 0 = 永不过期。" },
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

            // ============ B3.3: RecipeView 扩展（参考 WinCC RecipeView）============
            new PropertyDescriptor { Key = "allowEditRecord", DisplayName = "允许新增/删除记录", EditorType = PropertyEditorType.Boolean, DefaultValue = "true", Category = "行为" },
            new PropertyDescriptor { Key = "dataRecordTag", DisplayName = "当前记录索引 Tag", EditorType = PropertyEditorType.TagAddress, DefaultValue = "", Category = "数据",
                Description = "B3.3: 用于在多个控件间同步当前记录。WinCC DataRecordTag。" },
            new PropertyDescriptor { Key = "writeOnSelect", DisplayName = "选中时写 PLC", EditorType = PropertyEditorType.Boolean, DefaultValue = "false", Category = "行为",
                Description = "B3.3: 切换记录时自动下发到 PLC Tag。" },
            new PropertyDescriptor { Key = "readOnDeselect", DisplayName = "切走前回读", EditorType = PropertyEditorType.Boolean, DefaultValue = "false", Category = "行为" },
            new PropertyDescriptor
            {
                Key = "exportFormat", DisplayName = "导出格式", EditorType = PropertyEditorType.Enum, DefaultValue = "CSV", Category = "导出",
                EnumOptions = new[] { "CSV|CSV", "Excel|Excel", "XML|XML", "JSON|JSON" }
            },
            new PropertyDescriptor { Key = "showSearchBar", DisplayName = "显示搜索栏", EditorType = PropertyEditorType.Boolean, DefaultValue = "false", Category = "外观" },
        }
    };

    /// <summary>
    /// B1B: 注册 schema 时按 TypeId 自动追加 5 类通用字段（安全/闪烁/字体/边框/内边距）。
    /// 已存在同 Key 的字段不覆盖（widget 自有字段优先），保证向后兼容。
    /// </summary>
    private static void Add(Dictionary<string, WidgetSchema> map, WidgetSchema schema)
    {
        var existingKeys = new HashSet<string>(schema.Properties.Select(p => p.Key), System.StringComparer.OrdinalIgnoreCase);
        var extras = GetCommonForTypeId(schema.TypeId).Where(p => !existingKeys.Contains(p.Key));
        var merged = schema.Properties.Concat(extras).ToList();
        map[schema.TypeId] = schema with { Properties = merged };
    }

    /// <summary>
    /// B1B: 按 TypeId 决定该 widget 适用哪几类通用字段。
    /// 矩阵参考：docs/widget-properties/_remaining.md 与任务文档矩阵表。
    /// </summary>
    private static IEnumerable<PropertyDescriptor> GetCommonForTypeId(string typeId) => typeId switch
    {
        // 含文本+矩形外观+内边距：5 类全上
        "text" or "io-numeric" or "io-symbolic" or "datetime" or "button" or "round-button"
            or "switch" or "clock" or "combobox" or "listbox" or "checkbox" or "optiongroup"
            or "table-view"
            => AllCommon(),

        // 含文本但内边距不适用：字体 + 边框 + 闪烁 + 安全
        "gauge" => TextRectNoMarginCommon(),

        // 矩形外观（无文本）：边框 + 闪烁 + 安全
        "rectangle" or "ellipse" or "polygon" or "graphic-view" or "io-graphic"
            or "bar" or "slider" or "scrollbar" or "trend-view" or "alarm-view"
            or "alarm-indicator" or "status-force" or "recipe-view" or "user-view"
            or "diagnostic-view"
            => RectVisualCommon(),

        // 线性：只加安全
        "line" or "polyline" => LineOnlyCommon(),

        // 媒体/容器：边框 + 安全
        "html-browser" or "pdf-view" or "media-player" or "xy-trend" or "report-view"
        or "screen-window"
            => MediaCommon(),

        // 兜底：未识别 → 仅安全
        _ => CommonSecurityFields(),
    };

    // =========================================================================
    // B1B: 5 类通用字段（按 WinCC IOField Table 1-50 / Button Table 1-7 等通用属性）
    // 依据：WinCC ProgRef V18 PDF — Authorization / LogOperation / AskOperationMotive /
    //       OperatorEnable / Flashing / FlashingRate / Flashing*ColorOn/Off / Font* /
    //       BorderColor / BorderStyle / BorderWidth / BorderBackColor / *Margin
    // =========================================================================

    /// <summary>安全/审计：4 字段 × 适用 27 widget。</summary>
    private static IEnumerable<PropertyDescriptor> CommonSecurityFields() => new[]
    {
        new PropertyDescriptor { Key = "authorization", DisplayName = "操作授权", EditorType = PropertyEditorType.String,
            DefaultValue = "", Category = "安全", Description = "需要的权限组名（留空 = 无限制）。WinCC Authorization。" },
        new PropertyDescriptor { Key = "logOperation", DisplayName = "记录操作", EditorType = PropertyEditorType.Boolean,
            DefaultValue = "false", Category = "安全", Description = "操作时写审计 trail。WinCC LogOperation, PDF Page 794。" },
        new PropertyDescriptor { Key = "askOperationMotive", DisplayName = "操作前询问原因", EditorType = PropertyEditorType.Boolean,
            DefaultValue = "false", Category = "安全", Description = "弹框要求输入操作原因（GMP 合规）。WinCC AskOperationMotive。" },
        new PropertyDescriptor { Key = "operatorEnable", DisplayName = "允许操作员操作", EditorType = PropertyEditorType.Boolean,
            DefaultValue = "true", Category = "安全", Description = "WinCC OperatorEnable / Enabled。" },
    };

    /// <summary>闪烁：6 字段 × 适用 25 widget（line/polyline 除外）。</summary>
    private static IEnumerable<PropertyDescriptor> CommonFlashingFields() => new[]
    {
        new PropertyDescriptor { Key = "flashing", DisplayName = "闪烁模式", EditorType = PropertyEditorType.Enum,
            DefaultValue = "None", Category = "闪烁",
            EnumOptions = new[] { "None|无", "Standard|标准", "Strong|强烈" },
            Description = "WinCC Flashing / FlashingEnabled (PDF Page 699 附近)" },
        new PropertyDescriptor { Key = "flashingRate", DisplayName = "闪烁频率", EditorType = PropertyEditorType.Enum,
            DefaultValue = "Medium", Category = "闪烁",
            EnumOptions = new[] { "Slow|慢", "Medium|中", "Fast|快" },
            Description = "WinCC FlashingRate, PDF Page 699" },
        new PropertyDescriptor { Key = "flashingBackgroundColorOn", DisplayName = "闪烁开背景色", EditorType = PropertyEditorType.Color,
            DefaultValue = "#FFFF00", Category = "闪烁", Description = "WinCC BackFlashingColorOn" },
        new PropertyDescriptor { Key = "flashingBackgroundColorOff", DisplayName = "闪烁关背景色", EditorType = PropertyEditorType.Color,
            DefaultValue = "#FFFFFF", Category = "闪烁", Description = "WinCC BackFlashingColorOff" },
        new PropertyDescriptor { Key = "flashingForegroundColorOn", DisplayName = "闪烁开前景色", EditorType = PropertyEditorType.Color,
            DefaultValue = "#000000", Category = "闪烁", Description = "WinCC ForeFlashingColorOn" },
        new PropertyDescriptor { Key = "flashingForegroundColorOff", DisplayName = "闪烁关前景色", EditorType = PropertyEditorType.Color,
            DefaultValue = "#000000", Category = "闪烁", Description = "WinCC ForeFlashingColorOff" },
    };

    /// <summary>字体细节：5 字段 × 适用 18 含文本 widget。</summary>
    private static IEnumerable<PropertyDescriptor> CommonFontDetailFields() => new[]
    {
        new PropertyDescriptor { Key = "fontFamily", DisplayName = "字体", EditorType = PropertyEditorType.String,
            DefaultValue = "Microsoft YaHei UI", Category = "文本格式",
            Description = "WinCC FontName, PDF Page 709" },
        new PropertyDescriptor { Key = "fontBold", DisplayName = "粗体", EditorType = PropertyEditorType.Boolean,
            DefaultValue = "false", Category = "文本格式",
            Description = "WinCC FontBold, PDF Page 708。fontWeight 字段优先于此。" },
        new PropertyDescriptor { Key = "fontItalic", DisplayName = "斜体", EditorType = PropertyEditorType.Boolean,
            DefaultValue = "false", Category = "文本格式",
            Description = "WinCC FontItalic, PDF Page 708" },
        new PropertyDescriptor { Key = "fontUnderline", DisplayName = "下划线", EditorType = PropertyEditorType.Boolean,
            DefaultValue = "false", Category = "文本格式", Description = "WinCC FontUnderline" },
        new PropertyDescriptor { Key = "fontStrikeThrough", DisplayName = "删除线", EditorType = PropertyEditorType.Boolean,
            DefaultValue = "false", Category = "文本格式", Description = "WinCC 扩展（ApexHMI 增项）" },
    };

    /// <summary>边框精细化：4 字段 × 适用 22 矩形外观 widget。</summary>
    private static IEnumerable<PropertyDescriptor> CommonBorderFields() => new[]
    {
        new PropertyDescriptor { Key = "borderColor", DisplayName = "边框颜色", EditorType = PropertyEditorType.Color,
            DefaultValue = "#CBD5E1", Category = "边框", Description = "WinCC BorderColor" },
        new PropertyDescriptor { Key = "borderStyle", DisplayName = "边框样式", EditorType = PropertyEditorType.Enum,
            DefaultValue = "Solid", Category = "边框",
            EnumOptions = new[] { "None|无", "Solid|实心", "Dashed|虚线", "Dotted|点线", "Double|双线" },
            Description = "WinCC BorderStyle, PDF Page 576" },
        new PropertyDescriptor { Key = "borderWidth", DisplayName = "边框宽度", EditorType = PropertyEditorType.Integer,
            DefaultValue = "1", Category = "边框", Description = "WinCC BorderWidth, PDF Page 577" },
        new PropertyDescriptor { Key = "borderBackColor", DisplayName = "边框背景色", EditorType = PropertyEditorType.Color,
            DefaultValue = "#FFFFFF", Category = "边框", Description = "虚线/点线模式的间隙色。WinCC BorderBackColor。" },
    };

    /// <summary>内边距：4 字段 × 适用 18 含文本 widget。</summary>
    private static IEnumerable<PropertyDescriptor> CommonMarginFields() => new[]
    {
        new PropertyDescriptor { Key = "topMargin",    DisplayName = "上内边距", EditorType = PropertyEditorType.Integer,
            DefaultValue = "4", Category = "布局", Description = "WinCC TopMargin" },
        new PropertyDescriptor { Key = "bottomMargin", DisplayName = "下内边距", EditorType = PropertyEditorType.Integer,
            DefaultValue = "4", Category = "布局", Description = "WinCC BottomMargin" },
        new PropertyDescriptor { Key = "leftMargin",   DisplayName = "左内边距", EditorType = PropertyEditorType.Integer,
            DefaultValue = "6", Category = "布局", Description = "WinCC LeftMargin" },
        new PropertyDescriptor { Key = "rightMargin",  DisplayName = "右内边距", EditorType = PropertyEditorType.Integer,
            DefaultValue = "6", Category = "布局", Description = "WinCC RightMargin" },
    };

    /// <summary>组合所有 5 类（最全配置，主要用于含文本+矩形外观的 widget）。</summary>
    private static IEnumerable<PropertyDescriptor> AllCommon() =>
        CommonFontDetailFields()
            .Concat(CommonBorderFields())
            .Concat(CommonMarginFields())
            .Concat(CommonFlashingFields())
            .Concat(CommonSecurityFields());

    /// <summary>仅边框+闪烁+安全（适用矩形但无文本的 widget，如 rectangle/ellipse/io-graphic/bar/slider/scrollbar/trend-view/alarm-view/graphic-view/polygon）。</summary>
    private static IEnumerable<PropertyDescriptor> RectVisualCommon() =>
        CommonBorderFields()
            .Concat(CommonFlashingFields())
            .Concat(CommonSecurityFields());

    /// <summary>含文本+矩形外观（gauge — 有刻度字体但内边距不太合适；为保守只去掉 margin）。</summary>
    private static IEnumerable<PropertyDescriptor> TextRectNoMarginCommon() =>
        CommonFontDetailFields()
            .Concat(CommonBorderFields())
            .Concat(CommonFlashingFields())
            .Concat(CommonSecurityFields());

    /// <summary>line / polyline — 不支持边框/闪烁/字体/内边距，只加安全字段。</summary>
    private static IEnumerable<PropertyDescriptor> LineOnlyCommon() => CommonSecurityFields();

    /// <summary>screen-window — 仅安全 + 边框（无闪烁、无字体、无内边距）。</summary>
    private static IEnumerable<PropertyDescriptor> ScreenWindowCommon() =>
        CommonBorderFields().Concat(CommonSecurityFields());

    /// <summary>html-browser / pdf-view / media-player / xy-trend / report-view — 仅安全+边框。</summary>
    private static IEnumerable<PropertyDescriptor> MediaCommon() =>
        CommonBorderFields().Concat(CommonSecurityFields());

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

            // ============ B2D: WinCC Button Toggle 一体化（Table 1-11）============
            new PropertyDescriptor
            {
                Key = "buttonMode", DisplayName = "按钮模式", EditorType = PropertyEditorType.Enum, DefaultValue = "Push", Category = "行为",
                EnumOptions = new[] { "Push|普通", "Toggle|切换" },
                Description = "Push=普通点击 / Momentary 子模式；Toggle=按 stateTag BOOL 双态切换（WinCC Button Mode）。"
            },
            new PropertyDescriptor { Key = "stateTag", DisplayName = "状态变量", EditorType = PropertyEditorType.TagAddress,
                DefaultValue = "", Category = "行为",
                Description = "Toggle 模式下双状态绑定的 BOOL 变量。点击时读取并写反值。" },
            new PropertyDescriptor { Key = "onText", DisplayName = "ON 文本", EditorType = PropertyEditorType.String,
                DefaultValue = "ON", Category = "文本",
                Description = "Toggle 模式下 stateTag=true 时显示文本（覆盖 text）。" },
            new PropertyDescriptor { Key = "offText", DisplayName = "OFF 文本", EditorType = PropertyEditorType.String,
                DefaultValue = "OFF", Category = "文本",
                Description = "Toggle 模式下 stateTag=false 时显示文本。" },
            new PropertyDescriptor { Key = "onPicture", DisplayName = "ON 图片", EditorType = PropertyEditorType.String,
                DefaultValue = "", Category = "外观",
                Description = "Toggle 模式 ON 状态图片路径（同 graphic-view 模式）。当前版本仅 schema，运行时未渲染图片。" },
            new PropertyDescriptor { Key = "offPicture", DisplayName = "OFF 图片", EditorType = PropertyEditorType.String,
                DefaultValue = "", Category = "外观", Description = "Toggle 模式 OFF 状态图片路径。" },
        }
    };

    // ---------------- io-numeric ----------------
    // B1A: mode 拆出 dataFormat（PDF Page 642 + Page 824）
    // - Mode = Input / Output（仅 2 选项；WinCC IOField 的 Mode 在 Input 时本身就允许显示+输入）
    // - DataFormat = Decimal / Binary / Hexadecimal / String / DateTime
    private static WidgetSchema BuildIoNumeric() => new()
    {
        TypeId = "io-numeric",
        Properties = new[]
        {
            new PropertyDescriptor { Key = "variable", DisplayName = "变量", EditorType = PropertyEditorType.TagAddress, DefaultValue = "", Category = "数据" },
            new PropertyDescriptor
            {
                Key = "mode", DisplayName = "模式", EditorType = PropertyEditorType.Enum, DefaultValue = "Output", Category = "数据",
                EnumOptions = new[] { "Input|输入", "Output|仅输出" },
                Description = "Input=允许显示+输入；Output=只读显示（WinCC IOField Mode, PDF Page 824）"
            },
            new PropertyDescriptor
            {
                Key = "dataFormat", DisplayName = "数据格式", EditorType = PropertyEditorType.Enum, DefaultValue = "Decimal", Category = "格式",
                EnumOptions = new[] { "Decimal|十进制", "Binary|二进制", "Hexadecimal|十六进制", "String|字符串", "DateTime|日期时间" },
                Description = "显示格式（WinCC IOField DataFormat, PDF Page 642）。Decimal 走 format 串；其余按类型转换。"
            },
            new PropertyDescriptor { Key = "format", DisplayName = "数值格式", EditorType = PropertyEditorType.String, DefaultValue = "0.##", Category = "格式", Description = ".NET 数值格式串（仅 dataFormat=Decimal 生效）" },
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

            // ============ B2B: WinCC IOField 高优 7 字段（Table 1-50）============
            new PropertyDescriptor { Key = "acceptOnExit", DisplayName = "失焦写入", EditorType = PropertyEditorType.Boolean,
                DefaultValue = "true", Category = "输入行为",
                Description = "失焦时是否写值（WinCC AcceptOnExit, PDF Page 528）。false 仅回车写。" },
            new PropertyDescriptor { Key = "acceptOnFull", DisplayName = "填满写入", EditorType = PropertyEditorType.Boolean,
                DefaultValue = "false", Category = "输入行为",
                Description = "输入填满 fieldLength 字符宽度时自动写值（WinCC AcceptOnFull）。" },
            new PropertyDescriptor { Key = "clearOnError", DisplayName = "错误时清空", EditorType = PropertyEditorType.Boolean,
                DefaultValue = "false", Category = "输入行为",
                Description = "输入越限或非法时清空（WinCC ClearOnError, PDF Page 564）。" },
            new PropertyDescriptor { Key = "clearOnFocus", DisplayName = "获焦时清空", EditorType = PropertyEditorType.Boolean,
                DefaultValue = "false", Category = "输入行为",
                Description = "TextBox 获焦时清空旧值（WinCC ClearOnFocus）。" },
            new PropertyDescriptor { Key = "aboveUpperLimitColor", DisplayName = "越上限背景色", EditorType = PropertyEditorType.Color,
                DefaultValue = "", Category = "限值色",
                Description = "当前值 > maxValue 时背景色（WinCC AboveUpperLimitColor, PDF Page 524）。留空 = 不切色。" },
            new PropertyDescriptor { Key = "belowLowerLimitColor", DisplayName = "越下限背景色", EditorType = PropertyEditorType.Color,
                DefaultValue = "", Category = "限值色",
                Description = "当前值 < minValue 时背景色（WinCC BelowLowerLimitColor）。" },
            new PropertyDescriptor { Key = "tooltipText", DisplayName = "Tooltip", EditorType = PropertyEditorType.String,
                DefaultValue = "", Category = "辅助",
                Description = "WinCC TooltipText。" },

            // ============ B2B: WinCC IOField 中优 8 字段 ============
            new PropertyDescriptor { Key = "fieldLength", DisplayName = "字符宽度", EditorType = PropertyEditorType.Integer,
                DefaultValue = "0", Category = "格式",
                Description = "显示字符宽度（WinCC FieldLength）。0 = 不限制。" },
            new PropertyDescriptor { Key = "cursorControl", DisplayName = "完成后流转", EditorType = PropertyEditorType.Enum,
                DefaultValue = "None", Category = "输入行为",
                EnumOptions = new[] { "None|无", "TabOrder|按 Tab 顺序" },
                Description = "WinCC CursorControl — 当前版本仅模型层，运行时未实现 Tab 链。" },
            new PropertyDescriptor { Key = "editOnFocus", DisplayName = "获焦直接编辑", EditorType = PropertyEditorType.Boolean,
                DefaultValue = "false", Category = "输入行为",
                Description = "Output 视图获焦时立即进入编辑模式（WinCC EditOnFocus）。" },
            new PropertyDescriptor { Key = "hiddenInput", DisplayName = "隐藏输入", EditorType = PropertyEditorType.Boolean,
                DefaultValue = "false", Category = "输入行为",
                Description = "输入字符显示为 *（WinCC HiddenInput, 密码风格）。" },
            new PropertyDescriptor { Key = "showTouchKeyboard", DisplayName = "触屏键盘", EditorType = PropertyEditorType.Boolean,
                DefaultValue = "false", Category = "输入行为",
                Description = "M3.3 — 获焦时弹出对应键盘（DataFormat=String 弹全键盘，其它弹数字键盘）。WinCC Screen keyboard RT Advanced。" },
            new PropertyDescriptor { Key = "formatPattern", DisplayName = "格式串", EditorType = PropertyEditorType.String,
                DefaultValue = "", Category = "格式",
                Description = "WinCC FormatPattern 例 '999.99'。留空走 format/decimals。" },
            new PropertyDescriptor { Key = "formatType", DisplayName = "格式类型(WinCC)", EditorType = PropertyEditorType.Enum,
                DefaultValue = "Decimal", Category = "格式",
                EnumOptions = new[] { "Decimal|十进制", "Binary|二进制", "Hexadecimal|十六进制", "DateTime|日期时间" },
                Description = "WinCC FormatType — 与 dataFormat 等价，dataFormat 优先。" },
            new PropertyDescriptor { Key = "unitColor", DisplayName = "单位文字颜色", EditorType = PropertyEditorType.Color,
                DefaultValue = "#64748B", Category = "格式",
                Description = "单位文本颜色（WinCC UnitColor）。当前版本不分色，留作 B3。" },
            new PropertyDescriptor { Key = "unitMargin", DisplayName = "单位左间距", EditorType = PropertyEditorType.Integer,
                DefaultValue = "4", Category = "格式",
                Description = "单位文字与数值之间的间距（WinCC UnitMargin）。" },
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
                EnumOptions = new[] { "Input|输入", "Output|仅输出" },
                Description = "B1A: WinCC SymbolicIOField Mode 只有 Input/Output 两值"
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

            // ============ B3.2: SwitchControl WinCC 扩展（PDF SwitchControl 章节）============
            new PropertyDescriptor { Key = "stateOnText", DisplayName = "ON 显示文本", EditorType = PropertyEditorType.String, DefaultValue = "", Category = "文本",
                Description = "B3.2: 覆盖 onText（仅在显示时使用，可写本地化 ${KEY}）。WinCC StateOnText。" },
            new PropertyDescriptor { Key = "stateOffText", DisplayName = "OFF 显示文本", EditorType = PropertyEditorType.String, DefaultValue = "", Category = "文本" },
            new PropertyDescriptor { Key = "stateOnPicture", DisplayName = "ON 图片", EditorType = PropertyEditorType.String, DefaultValue = "", Category = "外观",
                Description = "B3.2: ON 状态背景图片路径。WinCC StateOnPicture。" },
            new PropertyDescriptor { Key = "stateOffPicture", DisplayName = "OFF 图片", EditorType = PropertyEditorType.String, DefaultValue = "", Category = "外观" },
            new PropertyDescriptor { Key = "stateOnForeground", DisplayName = "ON 文字颜色", EditorType = PropertyEditorType.Color, DefaultValue = "#FFFFFF", Category = "外观" },
            new PropertyDescriptor { Key = "stateOffForeground", DisplayName = "OFF 文字颜色", EditorType = PropertyEditorType.Color, DefaultValue = "#0F172A", Category = "外观" },
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

            // ============ B2C: WinCC 5 级限值带（Bar PDF Table 1-7）============
            new PropertyDescriptor { Key = "useLimitBandColors", DisplayName = "启用 5 级限值带", EditorType = PropertyEditorType.Boolean,
                DefaultValue = "false", Category = "限值带",
                Description = "true 时优先使用 alarmHigh / warningHigh / toleranceHigh/Low / warningLow / alarmLow 6 阈值 + 4 色；false 沿用旧 warn/alarmThreshold。" },
            new PropertyDescriptor { Key = "alarmHigh", DisplayName = "报警上限", EditorType = PropertyEditorType.Number, DefaultValue = "100", Category = "限值带" },
            new PropertyDescriptor { Key = "warningHigh", DisplayName = "警告上限", EditorType = PropertyEditorType.Number, DefaultValue = "90", Category = "限值带" },
            new PropertyDescriptor { Key = "toleranceHigh", DisplayName = "容差上限", EditorType = PropertyEditorType.Number, DefaultValue = "80", Category = "限值带" },
            new PropertyDescriptor { Key = "toleranceLow", DisplayName = "容差下限", EditorType = PropertyEditorType.Number, DefaultValue = "20", Category = "限值带" },
            new PropertyDescriptor { Key = "warningLow", DisplayName = "警告下限", EditorType = PropertyEditorType.Number, DefaultValue = "10", Category = "限值带" },
            new PropertyDescriptor { Key = "alarmLow", DisplayName = "报警下限", EditorType = PropertyEditorType.Number, DefaultValue = "0", Category = "限值带" },
            new PropertyDescriptor { Key = "alarmHighColor", DisplayName = "报警色", EditorType = PropertyEditorType.Color, DefaultValue = "#DC2626", Category = "限值带" },
            new PropertyDescriptor { Key = "warningHighColor", DisplayName = "警告色", EditorType = PropertyEditorType.Color, DefaultValue = "#F59E0B", Category = "限值带" },
            new PropertyDescriptor { Key = "toleranceColor", DisplayName = "容差色", EditorType = PropertyEditorType.Color, DefaultValue = "#22C55E", Category = "限值带" },
            new PropertyDescriptor { Key = "normalColor", DisplayName = "常态色", EditorType = PropertyEditorType.Color, DefaultValue = "#3B82F6", Category = "限值带" },
            new PropertyDescriptor { Key = "colorChangeHysteresis", DisplayName = "切色防抖", EditorType = PropertyEditorType.Number,
                DefaultValue = "0", Category = "限值带",
                Description = "值在阈值附近抖动时保持旧色（生产现场必备）。0 = 关闭。" },
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

            // ============ B2C: WinCC 5 级限值带（Gauge PDF Table 1-42）============
            new PropertyDescriptor { Key = "useLimitBandColors", DisplayName = "启用 5 级限值带", EditorType = PropertyEditorType.Boolean,
                DefaultValue = "false", Category = "限值带" },
            new PropertyDescriptor { Key = "alarmHigh", DisplayName = "报警上限", EditorType = PropertyEditorType.Number, DefaultValue = "100", Category = "限值带" },
            new PropertyDescriptor { Key = "warningHigh", DisplayName = "警告上限", EditorType = PropertyEditorType.Number, DefaultValue = "90", Category = "限值带" },
            new PropertyDescriptor { Key = "toleranceHigh", DisplayName = "容差上限", EditorType = PropertyEditorType.Number, DefaultValue = "80", Category = "限值带" },
            new PropertyDescriptor { Key = "toleranceLow", DisplayName = "容差下限", EditorType = PropertyEditorType.Number, DefaultValue = "20", Category = "限值带" },
            new PropertyDescriptor { Key = "warningLow", DisplayName = "警告下限", EditorType = PropertyEditorType.Number, DefaultValue = "10", Category = "限值带" },
            new PropertyDescriptor { Key = "alarmLow", DisplayName = "报警下限", EditorType = PropertyEditorType.Number, DefaultValue = "0", Category = "限值带" },
            new PropertyDescriptor { Key = "alarmHighColor", DisplayName = "报警色", EditorType = PropertyEditorType.Color, DefaultValue = "#DC2626", Category = "限值带" },
            new PropertyDescriptor { Key = "warningHighColor", DisplayName = "警告色", EditorType = PropertyEditorType.Color, DefaultValue = "#F59E0B", Category = "限值带" },
            new PropertyDescriptor { Key = "toleranceColor", DisplayName = "容差色", EditorType = PropertyEditorType.Color, DefaultValue = "#22C55E", Category = "限值带" },
            new PropertyDescriptor { Key = "normalColor", DisplayName = "常态色", EditorType = PropertyEditorType.Color, DefaultValue = "#3B82F6", Category = "限值带" },
            new PropertyDescriptor { Key = "colorChangeHysteresis", DisplayName = "切色防抖", EditorType = PropertyEditorType.Number, DefaultValue = "0", Category = "限值带" },
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

            // ============ B2E: WinCC TrendControl 扩展（PDF Table 1-60）============
            // 多 Y 轴系统（最多 4 个）
            new PropertyDescriptor { Key = "yAxisCount", DisplayName = "Y 轴数", EditorType = PropertyEditorType.Integer, DefaultValue = "1", Category = "Y 轴" },
            new PropertyDescriptor { Key = "y1Min", DisplayName = "Y1 最小值", EditorType = PropertyEditorType.Number, DefaultValue = "0", Category = "Y 轴" },
            new PropertyDescriptor { Key = "y1Max", DisplayName = "Y1 最大值", EditorType = PropertyEditorType.Number, DefaultValue = "100", Category = "Y 轴" },
            new PropertyDescriptor { Key = "y1Color", DisplayName = "Y1 颜色", EditorType = PropertyEditorType.Color, DefaultValue = "#1F2937", Category = "Y 轴" },
            new PropertyDescriptor { Key = "y1Title", DisplayName = "Y1 标题", EditorType = PropertyEditorType.String, DefaultValue = "", Category = "Y 轴" },
            new PropertyDescriptor { Key = "y1Scale", DisplayName = "Y1 刻度", EditorType = PropertyEditorType.Enum, DefaultValue = "Linear", Category = "Y 轴",
                EnumOptions = new[] { "Linear|线性", "Logarithmic|对数" } },
            new PropertyDescriptor { Key = "y2Min", DisplayName = "Y2 最小值", EditorType = PropertyEditorType.Number, DefaultValue = "0", Category = "Y 轴" },
            new PropertyDescriptor { Key = "y2Max", DisplayName = "Y2 最大值", EditorType = PropertyEditorType.Number, DefaultValue = "100", Category = "Y 轴" },
            new PropertyDescriptor { Key = "y2Color", DisplayName = "Y2 颜色", EditorType = PropertyEditorType.Color, DefaultValue = "#DC2626", Category = "Y 轴" },
            new PropertyDescriptor { Key = "y2Title", DisplayName = "Y2 标题", EditorType = PropertyEditorType.String, DefaultValue = "", Category = "Y 轴" },
            new PropertyDescriptor { Key = "y2Scale", DisplayName = "Y2 刻度", EditorType = PropertyEditorType.Enum, DefaultValue = "Linear", Category = "Y 轴",
                EnumOptions = new[] { "Linear|线性", "Logarithmic|对数" } },
            new PropertyDescriptor { Key = "y3Min", DisplayName = "Y3 最小值", EditorType = PropertyEditorType.Number, DefaultValue = "0", Category = "Y 轴" },
            new PropertyDescriptor { Key = "y3Max", DisplayName = "Y3 最大值", EditorType = PropertyEditorType.Number, DefaultValue = "100", Category = "Y 轴" },
            new PropertyDescriptor { Key = "y3Color", DisplayName = "Y3 颜色", EditorType = PropertyEditorType.Color, DefaultValue = "#22C55E", Category = "Y 轴" },
            new PropertyDescriptor { Key = "y4Min", DisplayName = "Y4 最小值", EditorType = PropertyEditorType.Number, DefaultValue = "0", Category = "Y 轴" },
            new PropertyDescriptor { Key = "y4Max", DisplayName = "Y4 最大值", EditorType = PropertyEditorType.Number, DefaultValue = "100", Category = "Y 轴" },
            new PropertyDescriptor { Key = "y4Color", DisplayName = "Y4 颜色", EditorType = PropertyEditorType.Color, DefaultValue = "#F59E0B", Category = "Y 轴" },

            // 光标（Ruler）
            new PropertyDescriptor { Key = "showRuler", DisplayName = "显示光标", EditorType = PropertyEditorType.Boolean, DefaultValue = "false", Category = "光标" },
            new PropertyDescriptor { Key = "rulerColor", DisplayName = "光标颜色", EditorType = PropertyEditorType.Color, DefaultValue = "#0F172A", Category = "光标" },
            new PropertyDescriptor { Key = "rulerTimestamp", DisplayName = "光标时间(运行时)", EditorType = PropertyEditorType.String, DefaultValue = "", Category = "光标",
                Description = "运行时计算的当前光标时间戳。" },
            new PropertyDescriptor { Key = "rulerValues", DisplayName = "光标各曲线值(运行时)", EditorType = PropertyEditorType.String, DefaultValue = "", Category = "光标",
                Description = "运行时计算的当前光标位置各曲线值。" },

            // 工具栏细分
            new PropertyDescriptor { Key = "showZoom", DisplayName = "工具栏-缩放", EditorType = PropertyEditorType.Boolean, DefaultValue = "true", Category = "工具栏" },
            new PropertyDescriptor { Key = "showPan", DisplayName = "工具栏-平移", EditorType = PropertyEditorType.Boolean, DefaultValue = "true", Category = "工具栏" },
            new PropertyDescriptor { Key = "showExport", DisplayName = "工具栏-导出", EditorType = PropertyEditorType.Boolean, DefaultValue = "false", Category = "工具栏" },
            new PropertyDescriptor { Key = "showPauseResume", DisplayName = "工具栏-暂停/继续", EditorType = PropertyEditorType.Boolean, DefaultValue = "true", Category = "工具栏" },
        }
    };
}
