using System.Collections.Generic;

namespace ApexHMI.Models;

/// <summary>IO 配置表"轴名称"Sheet 中一行轴定义</summary>
public class AxisConfigEntry
{
    /// <summary>轴编号（0, 1, 2...）</summary>
    public int Index { get; set; }

    /// <summary>轴名称（如"虚主轴"、"气密测试S1轴"）</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>点位标签列表（有序，点位1→点位15），仅包含有内容的点位</summary>
    public List<AxisPointLabel> Points { get; set; } = new();
}

/// <summary>轴点位标签：点位序号 + 显示名称</summary>
public class AxisPointLabel
{
    public AxisPointLabel() { }
    public AxisPointLabel(int index, string label) { Index = index; Label = label; }

    /// <summary>PLC 点位序号（1~15，对应 PointSelect 写入值）</summary>
    public int Index { get; set; }

    /// <summary>显示标签（如"取料位"、"放料位"）</summary>
    public string Label { get; set; } = string.Empty;

    public override string ToString() => $"{Index}: {Label}";
}
