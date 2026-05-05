using System.Collections.Generic;

namespace ApexHMI.Models;

public class IoGenerationSettings
{
    public string PlcType { get; set; } = "汇川中型PLC";
    public string OperationNumber { get; set; } = "OP10";
    public int ControlDbMultiplier { get; set; } = 100;
    public int ControlDbOffset { get; set; } = 0;
    public int DriveDbOffset { get; set; } = 50;

    /// <summary>从"轴名称"Sheet 读取的轴定义，用于生成 Enum_OPxx_Axis 枚举文件。</summary>
    public List<AxisConfigEntry> AxisEntries { get; set; } = new List<AxisConfigEntry>();
}
