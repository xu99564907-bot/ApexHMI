#nullable enable
using System.Collections.Generic;
using ApexHMI.Models.RuntimeUi;

namespace ApexHMI.Services.RuntimeUi;

/// <summary>C3: 全局库内置示例样例。
/// <para>启动时若全局库为空，注入 27 个 widget 各一个 sensible 配置实例，
/// 方便客户演示与新用户上手。Category 统一为「内置样例」。</para>
/// </summary>
internal static class GlobalLibrarySamples
{
    /// <summary>构造 27 个示例 LibraryAsset。</summary>
    public static List<LibraryAsset> Build()
    {
        var list = new List<LibraryAsset>(27);

        // ===== 基础图元（5） =====
        list.Add(Make("text", "示例：标题文本", 200, 40, new Dictionary<string, string>
        {
            ["text"] = "标题文本",
            ["fontSize"] = "20",
            ["fontWeight"] = "Bold",
            ["foreground"] = "#0F172A",
            ["horizontalAlignment"] = "Center",
        }));
        list.Add(Make("rectangle", "示例：信息卡片底框", 200, 100, new Dictionary<string, string>
        {
            ["fill"] = "#F1F5F9",
            ["stroke"] = "#CBD5E1",
            ["strokeThickness"] = "1",
            ["cornerRadius"] = "6",
        }));
        list.Add(Make("ellipse", "示例：状态指示圆点", 24, 24, new Dictionary<string, string>
        {
            ["fill"] = "#22C55E",
            ["stroke"] = "#16A34A",
            ["strokeThickness"] = "1",
        }));
        list.Add(Make("line", "示例：分隔线", 200, 2, new Dictionary<string, string>
        {
            ["stroke"] = "#CBD5E1",
            ["strokeThickness"] = "1",
        }));
        list.Add(Make("polygon", "示例：警告三角形", 60, 60, new Dictionary<string, string>
        {
            ["points"] = "30,0 60,60 0,60",
            ["fill"] = "#FBBF24",
            ["stroke"] = "#B45309",
            ["strokeThickness"] = "2",
        }));

        // ===== 按钮 / 开关（4） =====
        list.Add(Make("button", "示例：状态指示按钮", 120, 36, new Dictionary<string, string>
        {
            ["text"] = "启动",
            ["mode"] = "toggle",
            ["onText"] = "运行",
            ["offText"] = "停止",
            ["onColor"] = "#22C55E",
            ["offColor"] = "#EF4444",
            ["foreground"] = "White",
            ["fontWeight"] = "SemiBold",
        }));
        list.Add(Make("round-button", "示例：急停按钮", 80, 80, new Dictionary<string, string>
        {
            ["text"] = "急停",
            ["fill"] = "#DC2626",
            ["foreground"] = "White",
            ["fontSize"] = "14",
            ["fontWeight"] = "Bold",
        }));
        list.Add(Make("switch", "示例：电源开关", 100, 40, new Dictionary<string, string>
        {
            ["onText"] = "ON",
            ["offText"] = "OFF",
            ["onColor"] = "#22C55E",
            ["offColor"] = "#94A3B8",
        }));
        list.Add(Make("checkbox", "示例：启用复选框", 120, 28, new Dictionary<string, string>
        {
            ["text"] = "启用功能",
            ["fontSize"] = "12",
        }));

        // ===== 数值 / 输入显示（4） =====
        list.Add(Make("io-numeric", "示例：温度数值显示", 140, 40, new Dictionary<string, string>
        {
            ["format"] = "0.00",
            ["unit"] = "°C",
            ["fontSize"] = "20",
            ["fontWeight"] = "SemiBold",
            ["foreground"] = "#0F172A",
            ["horizontalAlignment"] = "Right",
        }));
        list.Add(Make("io-symbolic", "示例：运行状态显示", 140, 32, new Dictionary<string, string>
        {
            ["valueMap"] = "0=停止;1=运行;2=故障",
            ["fontSize"] = "14",
            ["foreground"] = "#0F172A",
        }));
        list.Add(Make("io-graphic", "示例：图形状态指示", 80, 80, new Dictionary<string, string>
        {
            ["valueMap"] = "0=red;1=green;2=yellow",
        }));
        list.Add(Make("datetime", "示例：当前日期时间", 180, 32, new Dictionary<string, string>
        {
            ["format"] = "yyyy-MM-dd HH:mm:ss",
            ["fontSize"] = "13",
            ["foreground"] = "#334155",
        }));

        // ===== 棒图 / 量规 / 滑块（4） =====
        list.Add(Make("bar", "示例：水位棒图", 60, 200, new Dictionary<string, string>
        {
            ["minValue"] = "0",
            ["maxValue"] = "100",
            ["orientation"] = "Vertical",
            ["fill"] = "#0EA5E9",
            ["warnThreshold"] = "80",
            ["warnColor"] = "#F59E0B",
            ["unit"] = "%",
        }));
        list.Add(Make("gauge", "示例：圆盘量规", 180, 160, new Dictionary<string, string>
        {
            ["minValue"] = "0",
            ["maxValue"] = "100",
            ["unit"] = "%",
            ["warnLow"] = "20",
            ["warnHigh"] = "80",
            ["foreground"] = "#0F172A",
        }));
        list.Add(Make("slider", "示例：调速滑块", 220, 40, new Dictionary<string, string>
        {
            ["minValue"] = "0",
            ["maxValue"] = "100",
            ["value"] = "50",
            ["orientation"] = "Horizontal",
            ["showTicks"] = "true",
        }));
        list.Add(Make("scrollbar", "示例：水平滚动条", 220, 20, new Dictionary<string, string>
        {
            ["minValue"] = "0",
            ["maxValue"] = "100",
            ["orientation"] = "Horizontal",
        }));

        // ===== 时间 / 选择（4） =====
        list.Add(Make("clock", "示例：实时时钟", 180, 36, new Dictionary<string, string>
        {
            ["format"] = "HH:mm:ss",
            ["fontSize"] = "22",
            ["fontWeight"] = "Bold",
            ["foreground"] = "#0F172A",
        }));
        list.Add(Make("combobox", "示例：模式选择下拉框", 160, 32, new Dictionary<string, string>
        {
            ["items"] = "自动;手动;维护",
            ["fontSize"] = "12",
        }));
        list.Add(Make("listbox", "示例：配方列表", 200, 160, new Dictionary<string, string>
        {
            ["items"] = "配方A;配方B;配方C",
            ["fontSize"] = "12",
        }));
        list.Add(Make("optiongroup", "示例：单选组（速度档位）", 200, 120, new Dictionary<string, string>
        {
            ["items"] = "低速;中速;高速",
            ["fontSize"] = "12",
        }));

        // ===== 图形 / 折线 / 多边形（2） =====
        list.Add(Make("polyline", "示例：流程线（折线）", 200, 80, new Dictionary<string, string>
        {
            ["points"] = "0,40 60,10 120,40 200,10",
            ["stroke"] = "#2563EB",
            ["strokeThickness"] = "2",
        }));
        list.Add(Make("graphic-view", "示例：设备图形", 120, 80, new Dictionary<string, string>
        {
            ["stretch"] = "Uniform",
        }));

        // ===== 复合视图（4） =====
        list.Add(Make("trend-view", "示例：温度趋势图", 480, 240, new Dictionary<string, string>
        {
            ["title"] = "温度趋势",
            ["minValue"] = "0",
            ["maxValue"] = "100",
            ["unit"] = "°C",
            ["historyMinutes"] = "10",
        }));
        list.Add(Make("alarm-view", "示例：报警列表", 480, 240, new Dictionary<string, string>
        {
            ["title"] = "当前报警",
            ["pageSize"] = "20",
        }));
        list.Add(Make("table-view", "示例：数据表格", 480, 240, new Dictionary<string, string>
        {
            ["columns"] = "序号;名称;数值;单位",
            ["pageSize"] = "20",
        }));
        list.Add(Make("alarm-indicator", "示例：报警角标", 60, 60, new Dictionary<string, string>
        {
            ["activeColor"] = "#EF4444",
            ["inactiveColor"] = "#94A3B8",
            ["blink"] = "true",
        }));

        return list;
    }

    private static LibraryAsset Make(
        string typeId,
        string displayName,
        double width,
        double height,
        Dictionary<string, string> properties)
    {
        return new LibraryAsset
        {
            Name = displayName,
            Category = "内置样例",
            Widget = new WidgetInstance
            {
                TypeId = typeId,
                Width = width,
                Height = height,
                Properties = properties,
            },
        };
    }
}
