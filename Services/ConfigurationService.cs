using System.IO;
using System.Text.Json;
using ApexHMI.Interfaces;
using ApexHMI.Models;
using ApexHMI.Services.Security;

namespace ApexHMI.Services;

public class ConfigurationService : IConfigurationService
{
    private const string ConnectionFile = "connection.json";
    private const string IoGenerationFile = "io-generation.json";
    private const string TagsFile = "tags.json";
    private const string DesignDataFile = "design-data.json";
    private const string GitPullFile = "git-pull.json";

    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public async Task SaveAsync(string filePath, AppConfig config)
    {
        var configDir = Path.GetDirectoryName(filePath)!;
        Directory.CreateDirectory(configDir);

        var originals = SnapshotAndEncryptSecrets(config);
        try
        {
            using var stream = File.Create(filePath);
            await JsonSerializer.SerializeAsync(stream, config, _jsonOptions);
        }
        finally
        {
            RestoreSecrets(config, originals);
        }
    }

    public async Task<AppConfig?> LoadAsync(string filePath)
    {
        if (File.Exists(filePath))
        {
            using var stream = File.OpenRead(filePath);
            var config = await JsonSerializer.DeserializeAsync<AppConfig>(stream, _jsonOptions);
            if (config is null)
            {
                return null;
            }

            DecryptSecrets(config);
            return config;
        }

        var configDir = Path.GetDirectoryName(filePath)!;
        var connectionPath = Path.Combine(configDir, ConnectionFile);
        if (!File.Exists(connectionPath))
        {
            return null;
        }

        var migrated = await LoadSplitFilesAsync(configDir);
        DecryptSecrets(migrated);
        await SaveAsync(filePath, migrated);
        return migrated;
    }

    private async Task<AppConfig> LoadSplitFilesAsync(string configDir)
    {
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

    private async Task<T?> LoadPartitionAsync<T>(string configDir, string fileName) where T : class
    {
        var path = Path.Combine(configDir, fileName);
        if (!File.Exists(path)) return null;
        using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<T>(stream, _jsonOptions);
    }

    private static (string? Password, string? AccessToken) SnapshotAndEncryptSecrets(AppConfig config)
    {
        string? originalPassword = null;
        string? originalToken = null;

        if (config.Connection != null && !string.IsNullOrEmpty(config.Connection.Password))
        {
            originalPassword = config.Connection.Password;
            config.Connection.Password = SecretProtector.Protect(originalPassword) ?? string.Empty;
        }

        if (config.GitPull != null && !string.IsNullOrEmpty(config.GitPull.AccessToken))
        {
            originalToken = config.GitPull.AccessToken;
            config.GitPull.AccessToken = SecretProtector.Protect(originalToken) ?? string.Empty;
        }

        return (originalPassword, originalToken);
    }

    private static void RestoreSecrets(AppConfig config, (string? Password, string? AccessToken) originals)
    {
        if (originals.Password != null && config.Connection != null)
        {
            config.Connection.Password = originals.Password;
        }

        if (originals.AccessToken != null && config.GitPull != null)
        {
            config.GitPull.AccessToken = originals.AccessToken;
        }
    }

    private static void DecryptSecrets(AppConfig config)
    {
        if (config.Connection != null && !string.IsNullOrEmpty(config.Connection.Password))
        {
            config.Connection.Password = SecretProtector.Unprotect(config.Connection.Password) ?? string.Empty;
        }

        if (config.GitPull != null && !string.IsNullOrEmpty(config.GitPull.AccessToken))
        {
            config.GitPull.AccessToken = SecretProtector.Unprotect(config.GitPull.AccessToken) ?? string.Empty;
        }
    }
}
