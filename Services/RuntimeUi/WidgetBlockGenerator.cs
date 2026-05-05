using System;
using System.Collections.Generic;
using ApexHMI.Models.RuntimeUi;
using Serilog;

namespace ApexHMI.Services.RuntimeUi;

/// <summary>
/// 工业功能块批量生成器。
/// 支持气缸、电机、轴、机械手、挡停等标准功能块一键批量生成控件组。
/// </summary>
public class WidgetBlockGenerator
{
    private readonly IWidgetEditorService _widgetEditor;

    public WidgetBlockGenerator(IWidgetEditorService widgetEditor)
    {
        _widgetEditor = widgetEditor;
    }

    /// <summary>可用的功能块类型列表（用于 UI 选择）。</summary>
    public static readonly IReadOnlyList<string> BlockTypes = new[]
    {
        "cylinder", "motor", "axis", "robot", "stopper"
    };

    /// <summary>功能块的中文名称映射。</summary>
    public static readonly IReadOnlyDictionary<string, string> BlockTypeLabels = new Dictionary<string, string>
    {
        ["cylinder"] = "气缸功能块",
        ["motor"]    = "电机功能块",
        ["axis"]     = "轴功能块",
        ["robot"]    = "机械手功能块",
        ["stopper"]  = "挡停功能块",
    };

    /// <summary>每个功能块在水平布局下的宽度（px）。</summary>
    private static readonly Dictionary<string, double> BlockWidth = new()
    {
        ["cylinder"] = 220,
        ["motor"]    = 200,
        ["axis"]     = 240,
        ["robot"]    = 220,
        ["stopper"]  = 200,
    };

    /// <summary>每个功能块的高度（px）。</summary>
    private static readonly Dictionary<string, double> BlockHeight = new()
    {
        ["cylinder"] = 210,
        ["motor"]    = 190,
        ["axis"]     = 170,
        ["robot"]    = 190,
        ["stopper"]  = 160,
    };

    /// <summary>
    /// 批量生成功能块控件组。
    /// </summary>
    /// <param name="page">目标页面。</param>
    /// <param name="blockType">功能块类型（cylinder/motor/axis/robot/stopper）。</param>
    /// <param name="namePrefix">命名前缀，如 "CYL" → 生成 CYL1, CYL2 …</param>
    /// <param name="count">生成数量。</param>
    /// <param name="startX">起始 X 坐标。</param>
    /// <param name="startY">起始 Y 坐标。</param>
    /// <param name="horizontal">true=水平排列，false=垂直排列。</param>
    /// <param name="gapPx">功能块之间的间距（px）。</param>
    public IReadOnlyList<WidgetInstance> Generate(
        PageDefinition page,
        string blockType,
        string namePrefix,
        int count,
        double startX,
        double startY,
        bool horizontal = true,
        double gapPx = 16)
    {
        if (count <= 0) return Array.Empty<WidgetInstance>();

        var all = new List<WidgetInstance>();
        var bw = BlockWidth.TryGetValue(blockType, out var bwv) ? bwv : 200;
        var bh = BlockHeight.TryGetValue(blockType, out var bhv) ? bhv : 200;

        for (int i = 0; i < count; i++)
        {
            var name = $"{namePrefix}{i + 1}";
            var ox = startX + (horizontal ? i * (bw + gapPx) : 0);
            var oy = startY + (horizontal ? 0 : i * (bh + gapPx));

            var widgets = blockType.ToLowerInvariant() switch
            {
                "cylinder" => BuildCylinderBlock(page, name, ox, oy),
                "motor"    => BuildMotorBlock(page, name, ox, oy),
                "axis"     => BuildAxisBlock(page, name, ox, oy),
                "robot"    => BuildRobotBlock(page, name, ox, oy),
                "stopper"  => BuildStopperBlock(page, name, ox, oy),
                _          => Array.Empty<WidgetInstance>(),
            };

            all.AddRange(widgets);
        }

        Log.Information("WidgetBlockGenerator: 批量生成 blockType={BlockType} prefix={Prefix} count={Count}",
            blockType, namePrefix, count);
        return all;
    }

    // ---- 气缸功能块 ----
    // 布局：标题 / 前限位 / 后限位 / [前进] [退回]
    private IReadOnlyList<WidgetInstance> BuildCylinderBlock(PageDefinition page, string name, double x, double y)
    {
        var widgets = new List<WidgetInstance>
        {
            MakeText(page, name, x, y, 200, 28, "#1E40AF", "14", "SemiBold"),
            MakeBoolLamp(page, $"{name}_FwdSensor", $"{name} 前进到位", x, y + 36, 200, 30, "#22C55E"),
            MakeBoolLamp(page, $"{name}_BwdSensor", $"{name} 退回到位", x, y + 74, 200, 30, "#F59E0B"),
            MakeButton(page, $"{name} 前进", x, y + 116, 92, 36, "#2563EB",
                "write-bool", $"{name}_FwdCmd|True"),
            MakeButton(page, $"{name} 退回", x + 104, y + 116, 92, 36, "#64748B",
                "write-bool", $"{name}_FwdCmd|False"),
            MakeBoolLamp(page, $"{name}_Alarm", $"{name} 报警", x, y + 166, 200, 28, "#EF4444"),
        };
        return widgets;
    }

    // ---- 电机功能块 ----
    private IReadOnlyList<WidgetInstance> BuildMotorBlock(PageDefinition page, string name, double x, double y)
    {
        return new List<WidgetInstance>
        {
            MakeText(page, name, x, y, 180, 28, "#1E40AF", "14", "SemiBold"),
            MakeBoolLamp(page, $"{name}_Run", $"{name} 运行", x, y + 36, 180, 30, "#22C55E"),
            MakeBoolLamp(page, $"{name}_Fault", $"{name} 故障", x, y + 74, 180, 28, "#EF4444"),
            MakeButton(page, $"{name} 启动", x, y + 110, 82, 36, "#059669",
                "write-bool", $"{name}_StartCmd|True"),
            MakeButton(page, $"{name} 停止", x + 94, y + 110, 82, 36, "#B91C1C",
                "write-bool", $"{name}_StopCmd|True"),
        };
    }

    // ---- 轴功能块 ----
    private IReadOnlyList<WidgetInstance> BuildAxisBlock(PageDefinition page, string name, double x, double y)
    {
        return new List<WidgetInstance>
        {
            MakeText(page, name, x, y, 220, 28, "#1E40AF", "14", "SemiBold"),
            MakeNumeric(page, $"{name}_Pos", $"{name} 位置", "mm", x, y + 36, 220, 48),
            MakeBoolLamp(page, $"{name}_Alarm", $"{name} 报警", x, y + 92, 220, 28, "#EF4444"),
            MakeButton(page, "使能", x, y + 128, 100, 32, "#2563EB",
                "write-bool", $"{name}_Enable|True"),
            MakeButton(page, "复位", x + 112, y + 128, 100, 32, "#64748B",
                "write-bool", $"{name}_AlarmReset|True"),
        };
    }

    // ---- 机械手功能块 ----
    private IReadOnlyList<WidgetInstance> BuildRobotBlock(PageDefinition page, string name, double x, double y)
    {
        return new List<WidgetInstance>
        {
            MakeText(page, name, x, y, 200, 28, "#1E40AF", "14", "SemiBold"),
            MakeBoolLamp(page, $"{name}_Run", $"{name} 运行", x, y + 36, 200, 30, "#22C55E"),
            MakeBoolLamp(page, $"{name}_Pause", $"{name} 暂停", x, y + 74, 200, 30, "#F59E0B"),
            MakeBoolLamp(page, $"{name}_Fault", $"{name} 故障", x, y + 112, 200, 28, "#EF4444"),
            MakeButton(page, "暂停", x, y + 148, 92, 34, "#F59E0B",
                "write-bool", $"{name}_PauseCmd|True"),
            MakeButton(page, "复位", x + 104, y + 148, 92, 34, "#64748B",
                "write-bool", $"{name}_ResetCmd|True"),
        };
    }

    // ---- 挡停功能块 ----
    private IReadOnlyList<WidgetInstance> BuildStopperBlock(PageDefinition page, string name, double x, double y)
    {
        return new List<WidgetInstance>
        {
            MakeText(page, name, x, y, 180, 28, "#1E40AF", "14", "SemiBold"),
            MakeBoolLamp(page, $"{name}_Up", $"{name} 升起", x, y + 36, 180, 30, "#EF4444"),
            MakeBoolLamp(page, $"{name}_Down", $"{name} 落下", x, y + 74, 180, 30, "#22C55E"),
            MakeButton(page, "升起", x, y + 112, 82, 34, "#EF4444",
                "write-bool", $"{name}_UpCmd|True"),
            MakeButton(page, "落下", x + 94, y + 112, 82, 34, "#22C55E",
                "write-bool", $"{name}_DownCmd|True"),
        };
    }

    // ---- 辅助方法 ----

    private WidgetInstance MakeText(PageDefinition page, string text, double x, double y,
        double w, double h, string fg = "#0F172A", string fontSize = "13", string weight = "Normal")
    {
        var w2 = _widgetEditor.AddWidget(page, "text", x, y);
        _widgetEditor.ResizeWidget(w2, w, h);
        _widgetEditor.UpdateProperty(w2, "text", text);
        _widgetEditor.UpdateProperty(w2, "foreground", fg);
        _widgetEditor.UpdateProperty(w2, "fontSize", fontSize);
        _widgetEditor.UpdateProperty(w2, "fontWeight", weight);
        return w2;
    }

    private WidgetInstance MakeBoolLamp(PageDefinition page, string tagId, string label,
        double x, double y, double w, double h, string trueColor)
    {
        var wi = _widgetEditor.AddWidget(page, "bool-lamp", x, y);
        _widgetEditor.ResizeWidget(wi, w, h);
        _widgetEditor.UpdateProperty(wi, "label", label);
        _widgetEditor.UpdateProperty(wi, "trueColor", trueColor);
        _widgetEditor.UpdateBinding(wi, new BindingSpec
        {
            TagId = tagId,
            AccessMode = BindingAccessMode.Subscribe,
            DataType = "Bool"
        });
        return wi;
    }

    private WidgetInstance MakeButton(PageDefinition page, string text,
        double x, double y, double w, double h, string bg,
        string? actionType = null, string? actionParam = null)
    {
        var wi = _widgetEditor.AddWidget(page, "button", x, y);
        _widgetEditor.ResizeWidget(wi, w, h);
        _widgetEditor.UpdateProperty(wi, "text", text);
        _widgetEditor.UpdateProperty(wi, "background", bg);
        wi.ActionType = actionType;
        wi.ActionParam = actionParam;
        return wi;
    }

    private WidgetInstance MakeNumeric(PageDefinition page, string tagId, string label, string unit,
        double x, double y, double w, double h)
    {
        var wi = _widgetEditor.AddWidget(page, "numeric-readonly", x, y);
        _widgetEditor.ResizeWidget(wi, w, h);
        _widgetEditor.UpdateProperty(wi, "label", label);
        _widgetEditor.UpdateProperty(wi, "unit", unit);
        _widgetEditor.UpdateBinding(wi, new BindingSpec
        {
            TagId = tagId,
            AccessMode = BindingAccessMode.Subscribe,
            DataType = "Float"
        });
        return wi;
    }
}
