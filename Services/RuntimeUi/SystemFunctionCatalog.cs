#nullable enable
using System.Collections.Generic;
using System.Linq;

namespace ApexHMI.Services.RuntimeUi;

/// <summary>P1: 系统函数的一个参数描述。</summary>
public sealed record FunctionArg(string Key, string Label, FunctionArgType Type, string Placeholder = "");

/// <summary>P1: 函数参数的类型，决定 Inspector 中用什么编辑器。</summary>
public enum FunctionArgType
{
    /// <summary>Tag 地址，挂 TagAutoComplete。</summary>
    TagAddress,
    /// <summary>普通文本。</summary>
    Text,
    /// <summary>数值。</summary>
    Number,
    /// <summary>布尔，True/False 下拉。</summary>
    Boolean,
    /// <summary>页面 RouteKey，走 NavigateTargets 下拉。</summary>
    PageRouteKey,
}

/// <summary>P1: 一个系统函数的元数据描述。</summary>
public sealed record SystemFunction(
    string Id,
    string DisplayName,
    string Category,
    string Description,
    IReadOnlyList<FunctionArg> Args);

/// <summary>P1: 系统函数库（参考 TIA Portal「系统函数」分类）。</summary>
public static class SystemFunctionCatalog
{
    public static readonly IReadOnlyList<SystemFunction> All = new SystemFunction[]
    {
        // 编辑位
        new("set-bit",    "置位",       "编辑位", "把指定 BOOL Tag 置为 True",  new[] { new FunctionArg("address", "Tag 地址", FunctionArgType.TagAddress) }),
        new("reset-bit",  "复位",       "编辑位", "把指定 BOOL Tag 置为 False", new[] { new FunctionArg("address", "Tag 地址", FunctionArgType.TagAddress) }),
        new("toggle-bit", "取反位",     "编辑位", "读当前值后写反值",            new[] { new FunctionArg("address", "Tag 地址", FunctionArgType.TagAddress) }),
        // 兼容旧 set-on/off/toggle/momentary，运行时等价转换
        new("set-on",     "设为 ON",   "编辑位", "等同置位",                    new[] { new FunctionArg("address", "Tag 地址", FunctionArgType.TagAddress) }),
        new("set-off",    "设为 OFF",  "编辑位", "等同复位",                    new[] { new FunctionArg("address", "Tag 地址", FunctionArgType.TagAddress) }),
        new("toggle",     "切换开关",  "编辑位", "等同取反位",                  new[] { new FunctionArg("address", "Tag 地址", FunctionArgType.TagAddress) }),
        new("momentary",  "复归型/点动","编辑位", "按下写 True、松开写 False",   new[] { new FunctionArg("address", "Tag 地址", FunctionArgType.TagAddress) }),

        // 写入变量
        new("write-bool",  "写入布尔",   "写入变量", "写一个 True/False",          new[] { new FunctionArg("address", "Tag 地址", FunctionArgType.TagAddress), new FunctionArg("value", "值 (True/False)", FunctionArgType.Boolean) }),
        new("write-int",   "写入整数",   "写入变量", "写一个整数值",                new[] { new FunctionArg("address", "Tag 地址", FunctionArgType.TagAddress), new FunctionArg("value", "整数值", FunctionArgType.Number) }),
        new("write-float", "写入浮点",   "写入变量", "写一个浮点值",                new[] { new FunctionArg("address", "Tag 地址", FunctionArgType.TagAddress), new FunctionArg("value", "浮点值", FunctionArgType.Number) }),
        new("increment",   "加 1",       "写入变量", "Tag += 1（占位）",            new[] { new FunctionArg("address", "Tag 地址", FunctionArgType.TagAddress) }),
        new("decrement",   "减 1",       "写入变量", "Tag -= 1（占位）",            new[] { new FunctionArg("address", "Tag 地址", FunctionArgType.TagAddress) }),

        // 画面
        new("navigate",    "跳转页面",   "画面", "跳转到目标 RouteKey 或固定段",    new[] { new FunctionArg("routeKey", "目标", FunctionArgType.PageRouteKey) }),
        new("back",        "返回上一页", "画面", "回到上一个画面（占位）",          System.Array.Empty<FunctionArg>()),
        new("popup",       "打开弹窗",   "画面", "把目标画面作为弹窗打开",          new[] { new FunctionArg("routeKey", "目标", FunctionArgType.PageRouteKey) }),

        // 报警
        new("ack-current",  "确认当前报警", "报警", "确认当前选中报警（占位）",      System.Array.Empty<FunctionArg>()),
        new("clear-buffer", "清空报警缓冲", "报警", "清空所有报警记录（占位）",      System.Array.Empty<FunctionArg>()),

        // 其它
        new("show-dialog", "显示对话框", "其它", "弹出 MessageBox 文字",            new[] { new FunctionArg("text", "文字", FunctionArgType.Text) }),
        new("play-sound",  "播放声音",   "其它", "播放系统提示音（占位，未实现）",  new[] { new FunctionArg("file", "音频文件", FunctionArgType.Text) }),
    };

    public static SystemFunction? GetById(string id) =>
        All.FirstOrDefault(f => f.Id == id);
}
