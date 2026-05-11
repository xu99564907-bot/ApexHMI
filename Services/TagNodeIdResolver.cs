using System;
using System.Linq;

namespace ApexHMI.Services;

/// <summary>
/// 把 csv 中 NodeId 模板里的占位符替换为当前工位实际 DB 编号。
/// 占位符约定：
///   {OP}        → 当前工位数字（IoOperationNumber 的数字部分，如 "OP30" → "30"）
///   {OP_TERM}   → 终端工位（默认 80，固定）
///   {OP00}      → 当前工位 *100（如 OP10 → 1000，OP30 → 3000）  Control DB
///   {OP02}      → 当前工位 *100 + 2  Recipe DB
///   {OP03}      → 当前工位 *100 + 3  Count DB
///   {OP05}      → 当前工位 *100 + 5  Communication DB
///   {OP50}      → 当前工位 *100 + 50 DriveControl DB
///   {OP70}      → 当前工位 *100 + 70 Fault DB
///   {TERM00}    → 终端工位 *100 = 8000  (Control DB)
///   {TERM03}    → 8003  (Count DB, 全线总计数常用)
///
/// 例：
///   模板 "ns=4;s=|var|Application.DB{OP00}_Control.Mode.Status.AutoMode"
///   IoOperationNumber="OP30" → "ns=4;s=|var|Application.DB3000_Control.Mode.Status.AutoMode"
///
///   模板 "ns=4;s=|var|Application.DB{TERM03}_Count.OK.Total"
///   永远 → "ns=4;s=|var|Application.DB8003_Count.OK.Total"
/// </summary>
public static class TagNodeIdResolver
{
    public const int DefaultTerminalOp = 80;

    public static int ParseOpNumber(string? opNumber, int fallback = 10)
    {
        if (string.IsNullOrWhiteSpace(opNumber)) return fallback;
        var digits = new string(opNumber.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var n) && n > 0 ? n : fallback;
    }

    public static string Resolve(string template, string? opNumber, int terminalOp = DefaultTerminalOp)
    {
        if (string.IsNullOrEmpty(template)) return template;
        var op = ParseOpNumber(opNumber);
        var s = template;

        // 当前工位 DB 编号 = op * 100 + suffix
        s = s.Replace("{OP00}", (op * 100).ToString());
        s = s.Replace("{OP02}", (op * 100 + 2).ToString());
        s = s.Replace("{OP03}", (op * 100 + 3).ToString());
        s = s.Replace("{OP05}", (op * 100 + 5).ToString());
        s = s.Replace("{OP50}", (op * 100 + 50).ToString());
        s = s.Replace("{OP70}", (op * 100 + 70).ToString());

        // 终端工位 (OP80 默认) DB 编号
        s = s.Replace("{TERM00}", (terminalOp * 100).ToString());
        s = s.Replace("{TERM02}", (terminalOp * 100 + 2).ToString());
        s = s.Replace("{TERM03}", (terminalOp * 100 + 3).ToString());
        s = s.Replace("{TERM05}", (terminalOp * 100 + 5).ToString());
        s = s.Replace("{TERM50}", (terminalOp * 100 + 50).ToString());
        s = s.Replace("{TERM70}", (terminalOp * 100 + 70).ToString());

        // 数字本身（如 "OP{OP}_xxx" → "OP30_xxx"）
        s = s.Replace("{OP}", op.ToString());
        s = s.Replace("{OP_TERM}", terminalOp.ToString());

        return s;
    }
}
