using System.IO;
using System.Text.Json;
using ApexHMI.Interfaces;
using ApexHMI.Models;

namespace ApexHMI.Services;

public class ConfigurationService : IConfigurationService
{
    private const string ConnectionFile = "connection.json";
    private const string IoGenerationFile = "io-generation.json";
    private const string TagsFile = "tags.json";
    private const string DesignDataFile = "design-data.json";
    private const string GitPullFile = "git-pull.json";
    private const string LegacyFile = "appsettings.json";

    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    /// <summary>
    /// 保存配置到拆分文件。filePath 参数用于定位 config 目录（向后兼容调用签名）。
    /// </summary>
    public async Task SaveAsync(string filePath, AppConfig config)
    {
        var configDir = Path.GetDirectoryName(filePath)!;
        Directory.CreateDirectory(configDir);

        await SavePartitionAsync(configDir, ConnectionFile,
            new ConnectionConfig { Connection = config.Connection });

        await SavePartitionAsync(configDir, IoGenerationFile,
            new IoGenerationConfig { IoGeneration = config.IoGeneration });

        await SavePartitionAsync(configDir, TagsFile,
            new TagsConfig { Tags = config.Tags, EventBindings = config.EventBindings });

        await SavePartitionAsync(configDir, DesignDataFile,
            new DesignDataConfig
            {
                IoTableRows = config.IoTableRows,
                ManualCylinderBlocks = config.ManualCylinderBlocks,
                AxisConfigEntries = config.AxisConfigEntries
            });

        await SavePartitionAsync(configDir, GitPullFile,
            new GitPullConfig { GitPull = config.GitPull ?? new GitPullSettings() });
    }

    /// <summary>
    /// 从拆分文件加载配置。若拆分文件不存在但旧文件存在，自动迁移。
    /// filePath 参数用于定位 config 目录（向后兼容调用签名）。
    /// </summary>
    public async Task<AppConfig?> LoadAsync(string filePath)
    {
        var configDir = Path.GetDirectoryName(filePath)!;
        var connectionPath = Path.Combine(configDir, ConnectionFile);
        var legacyPath = Path.Combine(configDir, LegacyFile);

        // 迁移：旧文件存在且新拆分文件不存在
        if (File.Exists(legacyPath) && !File.Exists(connectionPath))
        {
            var legacy = await LoadLegacyAsync(legacyPath);
            if (legacy is null) return null;

            // 写入拆分文件
            await SaveAsync(filePath, legacy);
            // 备份旧文件
            var backupPath = legacyPath + ".backup";
            if (File.Exists(backupPath)) File.Delete(backupPath);
            File.Move(legacyPath, backupPath);
            return legacy;
        }

        // 拆分文件也不存在
        if (!File.Exists(connectionPath))
        {
            return null;
        }

        // 从拆分文件加载
        var config = new AppConfig();

        var conn = await LoadPartitionAsync<ConnectionConfig>(configDir, ConnectionFile);
        if (conn != null) config.Connection = conn.Connection;

        var ioGen = await LoadPartitionAsync<IoGenerationConfig>(configDir, IoGenerationFile);
        if (ioGen != null) config.IoGeneration = ioGen.IoGeneration;

        var tags = await LoadPartitionAsync<TagsConfig>(configDir, TagsFile);
        if (tags != null)
        {
            config.Tags = tags.Tags;
            config.EventBindings = tags.EventBindings;
        }

        var design = await LoadPartitionAsync<DesignDataConfig>(configDir, DesignDataFile);
        if (design != null)
        {
            config.IoTableRows = design.IoTableRows;
            config.ManualCylinderBlocks = design.ManualCylinderBlocks;
            config.AxisConfigEntries = design.AxisConfigEntries;
        }

        var gitPull = await LoadPartitionAsync<GitPullConfig>(configDir, GitPullFile);
        if (gitPull != null)
        {
            config.GitPull = gitPull.GitPull ?? new GitPullSettings();
        }

        return config;
    }

    // ---- 内部辅助方法 ----

    private async Task SavePartitionAsync<T>(string configDir, string fileName, T data)
    {
        var path = Path.Combine(configDir, fileName);
        using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, data, _jsonOptions);
    }

    private async Task<T?> LoadPartitionAsync<T>(string configDir, string fileName) where T : class
    {
        var path = Path.Combine(configDir, fileName);
        if (!File.Exists(path)) return null;
        using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<T>(stream, _jsonOptions);
    }

    /// <summary>读取旧版单文件格式</summary>
    private async Task<AppConfig?> LoadLegacyAsync(string filePath)
    {
        if (!File.Exists(filePath)) return null;
        using var stream = File.OpenRead(filePath);
        return await JsonSerializer.DeserializeAsync<AppConfig>(stream, _jsonOptions);
    }
}
