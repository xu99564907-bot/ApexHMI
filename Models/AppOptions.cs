namespace ApexHMI.Models;

/// <summary>
/// Groups static application options that are injected through <c>IOptions&lt;T&gt;</c>.
/// </summary>
public class AppOptions
{
    /// <summary>Configuration file names and folders used by the application.</summary>
    public ConfigFileOptions ConfigFiles { get; set; } = new();

    /// <summary>OPC UA runtime tuning values.</summary>
    public OpcUaRuntimeOptions OpcUa { get; set; } = new();

    /// <summary>PLC program generation defaults.</summary>
    public IoProgramGenerationOptions IoProgramGeneration { get; set; } = new();

    /// <summary>生产计数模块班次定义（白班 / 夜班开始时间）。</summary>
    public ShiftOptions Shift { get; set; } = new();
}

/// <summary>
/// 班次配置。HMI 侧的生产计数按这两个时间点切分白班 / 夜班。
/// 默认 08:30 / 20:30 跟传统 PLC 端配置一致；可在 config/shift.json 改。
/// </summary>
public class ShiftOptions
{
    /// <summary>白班开始时间（24h，"HH:mm"）。</summary>
    public string DayStart { get; set; } = "08:30";

    /// <summary>夜班开始时间（24h，"HH:mm"）。</summary>
    public string NightStart { get; set; } = "20:30";

    /// <summary>SQLite 计数账本文件名（相对 ConfigDirectoryName）。</summary>
    public string DatabaseFileName { get; set; } = "production.db";
}

/// <summary>
/// File and folder names for application configuration artifacts.
/// </summary>
public class ConfigFileOptions
{
    /// <summary>Root configuration folder name relative to the application root.</summary>
    public string ConfigDirectoryName { get; set; } = "config";

    /// <summary>Main application settings file name.</summary>
    public string AppSettingsFileName { get; set; } = "appsettings.json";

    /// <summary>JSON Schema file name for the main application settings file.</summary>
    public string AppSettingsSchemaFileName { get; set; } = "appsettings.schema.json";

    /// <summary>Naming rules file name.</summary>
    public string NamingRulesFileName { get; set; } = "naming-rules.json";

    /// <summary>Designer layout file name.</summary>
    public string DesignerLayoutFileName { get; set; } = "designer-layout.json";

    /// <summary>Designer project file name.</summary>
    public string DesignerProjectFileName { get; set; } = "designer-project.json";

    /// <summary>OPC UA resolved node cache file name.</summary>
    public string OpcResolvedNodeCacheFileName { get; set; } = "opc-resolved-node-cache.json";
}

/// <summary>
/// Runtime tuning values for OPC UA connection, browsing, and tag resolution.
/// </summary>
public class OpcUaRuntimeOptions
{
    /// <summary>Maximum time to wait for a single connection attempt.</summary>
    public int ConnectAttemptTimeoutMs { get; set; } = 15000;

    /// <summary>Maximum number of tags to resolve in parallel.</summary>
    public int ResolveParallelism { get; set; } = 8;

    /// <summary>Maximum number of candidate node ids to probe while resolving one tag.</summary>
    public int MaxNodeIdProbeCandidates { get; set; } = 48;

    /// <summary>Whether OPC UA performance traces are written to the debug trace.</summary>
    public bool EnablePerformanceTrace { get; set; } = true;
}

/// <summary>
/// Defaults used when generating PLC object programs from IO tables.
/// </summary>
public class IoProgramGenerationOptions
{
    /// <summary>Default IP address inserted into Epson robot program templates.</summary>
    public string EpsonRobotIp { get; set; } = "192.168.0.10";

    /// <summary>Default port inserted into Epson robot program templates.</summary>
    public int EpsonRobotPort { get; set; } = 5000;

    /// <summary>Default IP address inserted into Kuka robot program templates.</summary>
    public string KukaRobotIp { get; set; } = "192.168.0.20";

    /// <summary>Default port inserted into Kuka robot program templates.</summary>
    public int KukaRobotPort { get; set; } = 7000;
}
