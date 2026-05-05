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
    /// <summary>所有工位的 SFC 自动程序配置</summary>
    public List<SfcProgramConfig> SfcPrograms { get; set; } = new();
    /// <summary>初始化程序配置（ACT_InitSTxx.st）</summary>
    public SfcProgramConfig? SfcInitProgram { get; set; }
    /// <summary>已废弃，仅用于从旧版本迁移</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public SfcProgramConfig? SfcProgram { get; set; }
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

// ========== SFC 自动程序配置 DTO ==========

/// <summary>SFC 自动程序完整配置（嵌入 AppConfig.SfcProgram）</summary>
public class SfcProgramConfig
{
    public string ProgramName { get; set; } = string.Empty;
    public string StationNo { get; set; } = "1";
    public List<SfcStepDto> Steps { get; set; } = new();
}

public class SfcStepDto
{
    public int StepNo { get; set; }
    public string CompletionCondition { get; set; } = string.Empty;
    public string NextStep { get; set; } = "END";
    public List<SfcStepActionDto> Actions { get; set; } = new();
    public List<SfcStepBranchDto> Branches { get; set; } = new();
    public List<SfcStepAlarmDto> Alarms { get; set; } = new();
}

public class SfcStepAlarmDto
{
    public string AlarmMessage { get; set; } = string.Empty;
    public string AlarmCondition { get; set; } = string.Empty;
    public string AlarmType { get; set; } = "Stop";
}

public class SfcStepActionDto
{
    public string DeviceType { get; set; } = "Cylinder";
    public int DeviceIndex { get; set; } = 1;
    public string DeviceName { get; set; } = string.Empty;
    public string ActionType { get; set; } = "ToWork";
    public int PointIndex { get; set; } = 1;
    public string CustomCommand { get; set; } = string.Empty;
    public string CustomCondition { get; set; } = string.Empty;
}

public class SfcStepBranchDto
{
    public string Condition { get; set; } = string.Empty;
    public string TargetStep { get; set; } = "END";
}
