#nullable enable
using System.Collections.Generic;
using System.Linq;
using ApexHMI.Models.RuntimeUi;

namespace ApexHMI.Services.RuntimeUi;

/// <summary>
/// P7F: 极简内置 Faceplate（4 个）—— 气缸 / 轴 / 机械手 / 挡停。
/// <para>每个 Faceplate 仅有最小骨架（标题 + 关键按钮/灯 + 关键 IO），不复刻 P0 删除前的复杂业务卡片。
/// 留给用户自行复制 + 增强；复杂业务可在 v2.0 单独提供模板。</para>
/// </summary>
public static class BuiltInFaceplates
{
    public const string CylinderId = "cylinder-faceplate";
    public const string AxisId     = "axis-faceplate";
    public const string RobotId    = "robot-faceplate";
    public const string StopperId  = "stopper-faceplate";

    /// <summary>若 Library 不含某个内置 Id，则追加。</summary>
    public static void EnsureInjected(FaceplateLibrary lib)
    {
        var existing = new HashSet<string>(lib.Faceplates.Select(f => f.Id), System.StringComparer.Ordinal);
        if (!existing.Contains(CylinderId)) lib.Faceplates.Add(BuildCylinder());
        if (!existing.Contains(AxisId))     lib.Faceplates.Add(BuildAxis());
        if (!existing.Contains(RobotId))    lib.Faceplates.Add(BuildRobot());
        if (!existing.Contains(StopperId))  lib.Faceplates.Add(BuildStopper());
    }

    // ===== 工厂方法 =====

    private static FaceplateProperty Prop(string key, string name, FaceplatePropertyType type, string def = "") =>
        new() { Key = key, DisplayName = name, Type = type, DefaultValue = def };

    private static WidgetInstance Text(double x, double y, double w, double h, string text, int fontSize = 13, string fg = "#0F172A") => new()
    {
        TypeId = "text",
        X = x, Y = y, Width = w, Height = h,
        Properties =
        {
            ["text"] = text,
            ["fontSize"] = fontSize.ToString(),
            ["foreground"] = fg,
            ["textAlign"] = "Center",
            ["verticalAlign"] = "Center",
        }
    };

    private static WidgetInstance Button(double x, double y, double w, double h, string text, string bg, string action, string addr) => new()
    {
        TypeId = "button",
        X = x, Y = y, Width = w, Height = h,
        Properties =
        {
            ["text"] = text,
            ["background"] = bg,
            ["foreground"] = "#FFFFFF",
        },
        ActionType = action,
        ActionParam = addr,
    };

    private static WidgetInstance Lamp(double x, double y, double size, string variable, string onColor = "#10B981", string offColor = "#94A3B8") => new()
    {
        TypeId = "ellipse",
        X = x, Y = y, Width = size, Height = size,
        Properties =
        {
            ["fill"] = offColor,
            ["stroke"] = "#475569",
            ["strokeThickness"] = "1",
        },
        Binding = new BindingSpec { TagId = variable, AccessMode = BindingAccessMode.Subscribe, DataType = "Boolean" },
        Appearance = new AppearanceAnimation
        {
            TagId = variable,
            MatchType = AppearanceMatchType.Range,
            Rows =
            {
                new AppearanceRow { RangeFrom = "1", RangeTo = "1", Background = onColor },
            }
        }
    };

    private static Faceplate BuildCylinder()
    {
        var fp = new Faceplate
        {
            Id = CylinderId,
            Name = "气缸",
            Version = "1.0.0",
            Category = "执行器",
            Description = "极简气缸：标题 + Work/Home 按钮 + 状态灯",
            DefaultWidth = 200,
            DefaultHeight = 120,
            IconKind = "Engine",
            IsBuiltIn = true,
        };
        fp.InterfaceProperties.Add(Prop("displayName", "显示名称", FaceplatePropertyType.String, "气缸"));
        fp.InterfaceProperties.Add(Prop("workTag",     "动作地址", FaceplatePropertyType.TagAddress));
        fp.InterfaceProperties.Add(Prop("homeTag",     "原位地址", FaceplatePropertyType.TagAddress));
        fp.InnerScreen = new PageDefinition
        {
            Title = "气缸内部",
            CanvasWidth = 200, CanvasHeight = 120,
            Widgets =
            {
                Text(8, 6, 184, 24, "{prop:displayName}", 14),
                Lamp(86, 36, 28, "{prop:workTag}"),
                Button(8, 76, 88, 36, "Work", "#2563EB", "set-on", "{prop:workTag}"),
                Button(104, 76, 88, 36, "Home", "#475569", "set-on", "{prop:homeTag}"),
            }
        };
        return fp;
    }

    private static Faceplate BuildAxis()
    {
        var fp = new Faceplate
        {
            Id = AxisId,
            Name = "轴",
            Version = "1.0.0",
            Category = "执行器",
            Description = "极简轴：标题 + 位置数值 + 启停按钮",
            DefaultWidth = 220,
            DefaultHeight = 140,
            IconKind = "AxisArrow",
            IsBuiltIn = true,
        };
        fp.InterfaceProperties.Add(Prop("displayName", "显示名称", FaceplatePropertyType.String, "轴"));
        fp.InterfaceProperties.Add(Prop("posTag",      "位置地址", FaceplatePropertyType.TagAddress));
        fp.InterfaceProperties.Add(Prop("startTag",    "启动地址", FaceplatePropertyType.TagAddress));
        fp.InterfaceProperties.Add(Prop("stopTag",     "停止地址", FaceplatePropertyType.TagAddress));
        var posDisplay = new WidgetInstance
        {
            TypeId = "io-numeric",
            X = 8, Y = 36, Width = 204, Height = 32,
            Properties =
            {
                ["mode"] = "Output",
                ["variable"] = "{prop:posTag}",
                ["format"] = "0.##",
                ["decimals"] = "2",
                ["unit"] = "mm",
                ["background"] = "#F1F5F9",
                ["foreground"] = "#0F172A",
                ["textAlign"] = "Center",
            },
            Binding = new BindingSpec { TagId = "{prop:posTag}", AccessMode = BindingAccessMode.Subscribe, DataType = "Double" },
        };
        fp.InnerScreen = new PageDefinition
        {
            Title = "轴内部",
            CanvasWidth = 220, CanvasHeight = 140,
            Widgets =
            {
                Text(8, 4, 204, 24, "{prop:displayName}", 14),
                posDisplay,
                Button(8, 92, 98, 40, "启动", "#10B981", "set-on", "{prop:startTag}"),
                Button(114, 92, 98, 40, "停止", "#EF4444", "set-on", "{prop:stopTag}"),
            }
        };
        return fp;
    }

    private static Faceplate BuildRobot()
    {
        var fp = new Faceplate
        {
            Id = RobotId,
            Name = "机械手",
            Version = "1.0.0",
            Category = "执行器",
            Description = "极简机械手：标题 + 状态文本 + Home 按钮",
            DefaultWidth = 200,
            DefaultHeight = 120,
            IconKind = "Robot",
            IsBuiltIn = true,
        };
        fp.InterfaceProperties.Add(Prop("displayName", "显示名称", FaceplatePropertyType.String, "机械手"));
        fp.InterfaceProperties.Add(Prop("statusTag",   "状态地址", FaceplatePropertyType.TagAddress));
        fp.InterfaceProperties.Add(Prop("homeTag",     "回原地址", FaceplatePropertyType.TagAddress));
        var statusText = new WidgetInstance
        {
            TypeId = "io-symbolic",
            X = 8, Y = 36, Width = 184, Height = 32,
            Properties =
            {
                ["mode"] = "Output",
                ["variable"] = "{prop:statusTag}",
                ["entries"] = "0=空闲;1=运行;2=报警",
                ["background"] = "#F1F5F9",
                ["foreground"] = "#0F172A",
            },
            Binding = new BindingSpec { TagId = "{prop:statusTag}", AccessMode = BindingAccessMode.Subscribe, DataType = "Int32" },
        };
        fp.InnerScreen = new PageDefinition
        {
            Title = "机械手内部",
            CanvasWidth = 200, CanvasHeight = 120,
            Widgets =
            {
                Text(8, 4, 184, 24, "{prop:displayName}", 14),
                statusText,
                Button(8, 76, 184, 36, "回原位", "#475569", "set-on", "{prop:homeTag}"),
            }
        };
        return fp;
    }

    private static Faceplate BuildStopper()
    {
        var fp = new Faceplate
        {
            Id = StopperId,
            Name = "挡停",
            Version = "1.0.0",
            Category = "执行器",
            Description = "极简挡停：标题 + 升降按钮 + 上/下到位灯",
            DefaultWidth = 220,
            DefaultHeight = 140,
            IconKind = "ArrowVerticalLock",
            IsBuiltIn = true,
        };
        fp.InterfaceProperties.Add(Prop("displayName",   "显示名称",   FaceplatePropertyType.String, "挡停"));
        fp.InterfaceProperties.Add(Prop("upTag",         "升起地址",   FaceplatePropertyType.TagAddress));
        fp.InterfaceProperties.Add(Prop("downTag",       "落下地址",   FaceplatePropertyType.TagAddress));
        fp.InterfaceProperties.Add(Prop("upSensorTag",   "上到位传感", FaceplatePropertyType.TagAddress));
        fp.InterfaceProperties.Add(Prop("downSensorTag", "下到位传感", FaceplatePropertyType.TagAddress));
        fp.InnerScreen = new PageDefinition
        {
            Title = "挡停内部",
            CanvasWidth = 220, CanvasHeight = 140,
            Widgets =
            {
                Text(8, 4, 204, 24, "{prop:displayName}", 14),
                Text(8, 36, 80, 22, "上到位", 11, "#475569"),
                Lamp(96, 36, 22, "{prop:upSensorTag}"),
                Text(124, 36, 80, 22, "下到位", 11, "#475569"),
                Lamp(196, 36, 22, "{prop:downSensorTag}"),
                Button(8, 92, 98, 40, "升起", "#10B981", "set-on", "{prop:upTag}"),
                Button(114, 92, 98, 40, "落下", "#EF4444", "set-on", "{prop:downTag}"),
            }
        };
        return fp;
    }
}
