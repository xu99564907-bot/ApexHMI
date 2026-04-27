using System.Collections.Generic;

namespace ApexHMI.Models;

public class AppConfig
{
    public OpcUaConnectionOptions Connection { get; set; } = new();
    public List<TagItem> Tags { get; set; } = new();
    public List<EventBinding> EventBindings { get; set; } = new();
    public IoGenerationSettings IoGeneration { get; set; } = new();
    public List<IoTableRow> IoTableRows { get; set; } = new();
    public IoTableSourceInfo IoTableSource { get; set; } = new();
    public List<ManualCylinderBlockItem> ManualCylinderBlocks { get; set; } = new();
    public List<AxisConfigEntry> AxisConfigEntries { get; set; } = new();
    public GitPullSettings GitPull { get; set; } = new();
}

/// <summary>记录最近一次导入的 IO 表来源信息，便于重启后仍可直接“保存 IO 表”。</summary>
public class IoTableSourceInfo
{
    public string FilePath { get; set; } = string.Empty;
    public int EncodingCodePage { get; set; } = 65001;
    public List<string> Headers { get; set; } = new();
}

/// <summary>手动配置 - connection.json</summary>
public class ConnectionConfig
{
    public OpcUaConnectionOptions Connection { get; set; } = new();
}

/// <summary>手动配置 - io-generation.json</summary>
public class IoGenerationConfig
{
    public IoGenerationSettings IoGeneration { get; set; } = new();
}

/// <summary>自动导入数据 - tags.json</summary>
public class TagsConfig
{
    public List<TagItem> Tags { get; set; } = new();
    public List<EventBinding> EventBindings { get; set; } = new();
}

/// <summary>项目数据 - design-data.json</summary>
public class DesignDataConfig
{
    public List<IoTableRow> IoTableRows { get; set; } = new();
    public List<ManualCylinderBlockItem> ManualCylinderBlocks { get; set; } = new();
    public List<AxisConfigEntry> AxisConfigEntries { get; set; } = new();
}

/// <summary>Git 仓库配置 - git-pull.json</summary>
public class GitPullConfig
{
    public GitPullSettings GitPull { get; set; } = new();
}
