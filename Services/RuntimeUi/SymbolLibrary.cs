#nullable enable
using System.Collections.Generic;
using System.Linq;

namespace ApexHMI.Services.RuntimeUi;

/// <summary>P6D: 内置工业符号目录。每个符号对应一个 MaterialDesignIcon Kind 名，
/// 拖入画布时生成 graphic-view widget 并把 IconKind 写到 Properties["iconKind"]。
/// </summary>
public sealed record IndustrialSymbol(string Id, string Name, string Category, string IconKind);

public static class SymbolLibrary
{
    public static readonly IReadOnlyList<IndustrialSymbol> Symbols = new IndustrialSymbol[]
    {
        // 容器
        new("tank",        "罐",       "容器", "Storage"),
        new("silo",        "料仓",     "容器", "SilverwareVariant"),
        // 动力
        new("pump",        "泵",       "动力", "Pump"),
        new("motor",       "电机",     "动力", "Engine"),
        new("fan",         "风机",     "动力", "Fan"),
        // 管路 / 阀门
        new("valve",       "阀门",     "管路", "Valve"),
        new("valve-open",  "开阀",     "管路", "ValveOpen"),
        new("valve-closed","闭阀",     "管路", "ValveClosed"),
        new("pipe",        "管道",     "管路", "Pipe"),
        new("arrow-flow",  "流向箭头", "管路", "ArrowRightBold"),
        // 仪表
        new("gauge",       "压力表",   "仪表", "Gauge"),
        new("thermometer", "温度计",   "仪表", "Thermometer"),
        // 报警 / 状态
        new("warn",        "警告",     "状态", "Alert"),
        new("ok",          "正常",     "状态", "CheckCircle"),
        new("error",       "故障",     "状态", "AlertOctagon"),
    };

    public static IEnumerable<IGrouping<string, IndustrialSymbol>> Groups()
        => Symbols.GroupBy(s => s.Category);
}
