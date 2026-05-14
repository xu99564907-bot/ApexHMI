using System;
using System.Collections.Generic;

namespace ApexHMI.Models.RuntimeUi;

/// <summary>读时迁移钩子：将低版本 ProjectDocument 无损升级到当前版本。</summary>
public static class ProjectMigration
{
    public const int CurrentSchemaVersion = 1;

    /// <summary>对已反序列化的文档执行迁移；返回同一实例（已原地修改）。</summary>
    public static ProjectDocument Migrate(ProjectDocument doc)
    {
        // P1: 把旧 ActionType+ActionParam 自动迁移到 Events["click"]
        // P2: 把旧 Animations 列表自动拆分到 Appearance / Visibility / Movement
        foreach (var page in doc.Pages)
        {
            foreach (var w in page.Widgets)
            {
                MigrateLegacyEvents(w);
                MigrateLegacyAnimations(w);
                MigrateIoNumericMode(w);
            }
        }

        // P6A: 旧工程没有 Styles 节点，确保默认色板/字体注入
        doc.Styles ??= new StyleDefinitions();
        doc.Styles.EnsureDefaults();

        // P6B: 文本资源默认空集合
        doc.Texts ??= new TextResources();
        doc.Texts.EnsureDefaults();

        // P6C: 项目库默认空集合
        doc.Library ??= new ProjectLibrary();

        // P6E: 文本/图形列表资源默认空集合
        doc.Lists ??= new ListResources();

        // P7: Faceplate 库默认空集合
        doc.Faceplates ??= new FaceplateLibrary();

        // P8A: 配方库默认空集合
        doc.Recipes ??= new RecipeLibrary();
        // 注：P7F 内置 Faceplate 注入由 RuntimeProjectService 统一处理（避免 Models → Services 反向依赖）

        // 预留：后续版本在此添加 if (doc.SchemaVersion < N) 迁移块
        doc.SchemaVersion = CurrentSchemaVersion;
        return doc;
    }

    /// <summary>P1: 把单个 widget 的旧 ActionType/ActionParam 迁移到 Events["click"]。
    /// <para>已有 Events 数据（新格式）则跳过；ActionType 为空或 "无" 则跳过。</para>
    /// <para>旧字段保留不删，便于 V1 旧版可读、且 ButtonWidgetViewModel 支持 fallback。</para>
    /// </summary>
    public static void MigrateLegacyEvents(WidgetInstance w)
    {
        if (w.Events.Count > 0) return;
        if (string.IsNullOrEmpty(w.ActionType) || w.ActionType == "无") return;

        var args = ParseLegacyParam(w.ActionType!, w.ActionParam ?? string.Empty);
        w.Events["click"] = new List<ActionStep>
        {
            new() { FunctionId = w.ActionType!, Args = args }
        };
    }

    /// <summary>P2-V2: 把旧 <c>List&lt;WidgetAnimation&gt;</c> 按 TargetProperty 拆到新模型。
    /// <para>已有任一新模型字段（Appearance/Visibility/Movement）则跳过迁移，避免覆盖用户编辑。</para>
    /// <para>旧 Animations 列表本身保留不删，可作为遗留 fallback。</para>
    /// </summary>
    public static void MigrateLegacyAnimations(WidgetInstance w)
    {
        if (w.Animations is null || w.Animations.Count == 0) return;
        if (w.Appearance is not null || w.Visibility is not null || w.Movement is not null) return;

        foreach (var a in w.Animations)
        {
            var prop = (a.TargetProperty ?? string.Empty).ToLowerInvariant();
            switch (prop)
            {
                case "background":
                case "foreground":
                    MergeIntoAppearance(w, a, prop);
                    break;
                case "visibility":
                case "isvisible":
                case "visible":
                    AssignVisibility(w, a);
                    break;
                case "x":
                case "left":
                case "canvas.left":
                    AssignMove(w, a, isVertical: false);
                    break;
                case "y":
                case "top":
                case "canvas.top":
                    AssignMove(w, a, isVertical: true);
                    break;
                // 其他未识别 TargetProperty：保留在旧 Animations 不动
            }
        }
    }

    private static void MergeIntoAppearance(WidgetInstance w, WidgetAnimation a, string prop)
    {
        w.Appearance ??= new AppearanceAnimation { TagId = a.TagId, MatchType = AppearanceMatchType.Range };
        if (string.IsNullOrEmpty(w.Appearance.TagId)) w.Appearance.TagId = a.TagId;

        var row = new AppearanceRow();
        var op = (a.Op ?? "eq").ToLowerInvariant();
        switch (op)
        {
            case "true":
                row.RangeFrom = "1"; row.RangeTo = "1";
                break;
            case "false":
                row.RangeFrom = "0"; row.RangeTo = "0";
                break;
            case "eq":
            case "ne":
                row.RangeFrom = a.CompareTo;
                row.RangeTo = a.CompareTo;
                break;
            case "gt":
                row.RangeFrom = a.CompareTo; row.RangeTo = "99999999";
                break;
            case "gte":
                row.RangeFrom = a.CompareTo; row.RangeTo = "99999999";
                break;
            case "lt":
                row.RangeFrom = "-99999999"; row.RangeTo = a.CompareTo;
                break;
            case "lte":
                row.RangeFrom = "-99999999"; row.RangeTo = a.CompareTo;
                break;
            default:
                row.RangeFrom = a.CompareTo;
                row.RangeTo = a.CompareTo;
                break;
        }

        if (prop == "background") row.Background = a.TargetValue ?? string.Empty;
        else if (prop == "foreground") row.Foreground = a.TargetValue ?? string.Empty;

        w.Appearance.Rows.Add(row);
    }

    private static void AssignVisibility(WidgetInstance w, WidgetAnimation a)
    {
        if (w.Visibility is not null) return;
        var op = (a.Op ?? "true").ToLowerInvariant();
        var mode = op switch
        {
            "true"  => VisibilityMode.WhenTrue,
            "false" => VisibilityMode.WhenFalse,
            _       => VisibilityMode.WhenInRange,
        };
        w.Visibility = new VisibilityAnimation
        {
            TagId = a.TagId,
            Mode = mode,
            RangeFrom = a.CompareTo,
            RangeTo = a.CompareTo,
            Otherwise = string.Equals(a.TargetValue, "Disabled", StringComparison.OrdinalIgnoreCase)
                ? VisibilityOtherwise.Disabled
                : VisibilityOtherwise.Hidden,
        };
    }

    private static void AssignMove(WidgetInstance w, WidgetAnimation a, bool isVertical)
    {
        w.Movement ??= new MoveAnimation
        {
            MoveType = isVertical ? MoveType.Vertical : MoveType.Horizontal,
        };
        // 仅在尚未设过对应轴时填充（避免覆盖）
        if (isVertical)
        {
            w.Movement.TagIdY = a.TagId;
            if (double.TryParse(a.TargetValue, out var py)) w.Movement.PixelEndY = py;
            if (double.TryParse(a.CompareTo, out var ry))  w.Movement.RangeMaxY = ry;
        }
        else
        {
            w.Movement.TagIdX = a.TagId;
            if (double.TryParse(a.TargetValue, out var px)) w.Movement.PixelEndX = px;
            if (double.TryParse(a.CompareTo, out var rx))  w.Movement.RangeMaxX = rx;
        }
    }

    /// <summary>B1A: io-numeric 旧 mode=InputOutput 迁移 → mode=Input + dataFormat 默认 Decimal。
    /// <para>WinCC IOField Mode（PDF p824）只有 Input/Output 两值，InputOutput 是 ApexHMI 早期混淆设计。</para>
    /// <para>Input 本身允许显示+输入，所以语义无损。</para>
    /// </summary>
    public static void MigrateIoNumericMode(WidgetInstance w)
    {
        if (!string.Equals(w.TypeId, "io-numeric", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(w.TypeId, "io-symbolic", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(w.TypeId, "io-graphic", StringComparison.OrdinalIgnoreCase))
            return;

        if (w.Properties.TryGetValue("mode", out var mode) &&
            string.Equals(mode, "InputOutput", StringComparison.OrdinalIgnoreCase))
        {
            w.Properties["mode"] = "Input";
        }

        // 仅 io-numeric 需要 dataFormat；其它两个 IO widget 不引入
        if (string.Equals(w.TypeId, "io-numeric", StringComparison.OrdinalIgnoreCase)
            && !w.Properties.ContainsKey("dataFormat"))
        {
            w.Properties["dataFormat"] = "Decimal";
        }
    }

    private static Dictionary<string, string> ParseLegacyParam(string actionType, string param)
    {
        var args = new Dictionary<string, string>();
        switch (actionType)
        {
            case "set-bit":
            case "reset-bit":
            case "toggle-bit":
            case "set-on":
            case "set-off":
            case "toggle":
            case "momentary":
            case "increment":
            case "decrement":
                args["address"] = param;
                break;
            case "write-bool":
            case "write-int":
            case "write-float":
                var parts = param.Split('|');
                args["address"] = parts.Length > 0 ? parts[0] : "";
                args["value"]   = parts.Length > 1 ? parts[1] : "";
                break;
            case "navigate":
            case "popup":
                args["routeKey"] = param;
                break;
            case "show-dialog":
                args["text"] = param;
                break;
            case "play-sound":
                args["file"] = param;
                break;
            default:
                if (!string.IsNullOrEmpty(param))
                    args["param"] = param;
                break;
        }
        return args;
    }
}
