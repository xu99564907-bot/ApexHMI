using System.Collections.Generic;
using ApexHMI.Models;

namespace ApexHMI.Interfaces;

/// <summary>
/// IO 表解析器：将已分割的二维字符串表解析为 IoTableRow 列表。
/// 不涉及文件 I/O、编码检测等基础设施问题。
/// </summary>
public interface IIoTableParser
{
    /// <summary>
    /// 解析已按行/列分割的字符串表。
    /// </summary>
    /// <param name="sourceRows">每行是一个列字符串列表（已 trim）。</param>
    /// <returns>解析结果，包含确认的表头和标准化后的数据行。</returns>
    IoTableParseResult Parse(IReadOnlyList<IReadOnlyList<string>> sourceRows);
}
