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
        foreach (var page in doc.Pages)
        {
            foreach (var w in page.Widgets)
                MigrateLegacyEvents(w);
        }

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
