using System.Collections.Generic;

namespace ApexHMI.Models;

/// <summary>
/// IO 表解析器的输出结果。
/// </summary>
public class IoTableParseResult
{
    /// <summary>解析后确认的表头列名。</summary>
    public List<string> Headers { get; set; } = new();

    /// <summary>解析后的数据行。</summary>
    public List<IoTableRow> Rows { get; set; } = new();
}
