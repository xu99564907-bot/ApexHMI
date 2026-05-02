using ApexHMI.Models;
using ApexHMI.Services;
using ApexHMI.Services.Security;
using ApexHMI.Tests.TestHelpers;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace ApexHMI.Tests.Services;

public class SecurityAndConfigurationServiceTests
{
    [Fact]
    public void SecretProtectorRoundTripsProtectedText()
    {
        const string plain = "p@ss-token";

        var protectedValue = SecretProtector.Protect(plain);
        var unprotected = SecretProtector.Unprotect(protectedValue);

        Assert.NotEqual(plain, protectedValue);
        Assert.True(SecretProtector.IsProtected(protectedValue));
        Assert.Equal(plain, unprotected);
    }

    [Fact]
    public void SecretProtectorLeavesEmptyPlainAndAlreadyProtectedValuesUnchanged()
    {
        var protectedValue = SecretProtector.Protect("token");

        Assert.Null(SecretProtector.Protect(null));
        Assert.Equal(string.Empty, SecretProtector.Protect(string.Empty));
        Assert.Equal(protectedValue, SecretProtector.Protect(protectedValue));
        Assert.Equal("plain", SecretProtector.Unprotect("plain"));
    }

    [Fact]
    public void SecretProtectorReturnsCipherTextWhenProtectedPayloadIsInvalid()
    {
        const string invalidCipher = "ENC:not-base64";

        var unprotected = SecretProtector.Unprotect(invalidCipher);

        Assert.Equal(invalidCipher, unprotected);
    }

    [Fact]
    public async Task SaveAsyncWritesSingleAppSettingsAndKeepsMemorySecretsPlain()
    {
        using var tempDir = TempDir.Create();
        var path = Path.Combine(tempDir.Path, "config", "appsettings.json");
        var service = new ConfigurationService();
        var config = new AppConfig
        {
            Connection = new OpcUaConnectionOptions
            {
                Password = "opc-password",
                UseAnonymous = false,
                Username = "operator"
            },
            GitPull = new GitPullSettings
            {
                AccessToken = "git-token",
                RepositoryUrl = "https://example.test/repo.git"
            },
            Tags =
            {
                new TagItem { Name = "TagA", NodeId = "ns=2;s=TagA" }
            }
        };

        await service.SaveAsync(path, config);

        var appsettingsJson = File.ReadAllText(path);
        Assert.Equal("opc-password", config.Connection.Password);
        Assert.Equal("git-token", config.GitPull.AccessToken);
        Assert.Contains("ENC:", appsettingsJson);
        Assert.Contains("\"Connection\"", appsettingsJson);
        Assert.Contains("\"GitPull\"", appsettingsJson);
        Assert.Contains("\"Tags\"", appsettingsJson);
        Assert.False(File.Exists(Path.Combine(tempDir.Path, "config", "connection.json")));
        Assert.False(File.Exists(Path.Combine(tempDir.Path, "config", "git-pull.json")));
    }

    [Fact]
    public async Task LoadAsyncDecryptsSingleAppSettingsSecrets()
    {
        using var tempDir = TempDir.Create();
        var path = Path.Combine(tempDir.Path, "config", "appsettings.json");
        var service = new ConfigurationService();

        await service.SaveAsync(path, new AppConfig
        {
            Connection = new OpcUaConnectionOptions { Password = "opc-password", UseAnonymous = false },
            GitPull = new GitPullSettings { AccessToken = "git-token" }
        });

        var loaded = await service.LoadAsync(path);

        Assert.NotNull(loaded);
        Assert.Equal("opc-password", loaded!.Connection.Password);
        Assert.Equal("git-token", loaded.GitPull.AccessToken);
    }

    [Fact]
    public async Task LoadAsyncMigratesSplitFilesToSingleAppSettings()
    {
        using var tempDir = TempDir.Create();
        var path = Path.Combine(tempDir.Path, "config", "appsettings.json");
        var configDir = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(configDir);
        File.WriteAllText(Path.Combine(configDir, "connection.json"), JsonSerializer.Serialize(new ConnectionConfig
        {
            Connection = new OpcUaConnectionOptions { Password = "split-password", UseAnonymous = false }
        }));
        File.WriteAllText(Path.Combine(configDir, "git-pull.json"), JsonSerializer.Serialize(new GitPullConfig
        {
            GitPull = new GitPullSettings { AccessToken = "split-token" }
        }));
        File.WriteAllText(Path.Combine(configDir, "tags.json"), JsonSerializer.Serialize(new TagsConfig
        {
            Tags = { new TagItem { Name = "MigratedTag" } }
        }));
        var service = new ConfigurationService();

        var loaded = await service.LoadAsync(path);

        Assert.NotNull(loaded);
        Assert.Equal("split-password", loaded!.Connection.Password);
        Assert.Equal("split-token", loaded.GitPull.AccessToken);
        Assert.Single(loaded.Tags);
        Assert.Equal("MigratedTag", loaded.Tags[0].Name);
        Assert.True(File.Exists(path));
        Assert.Contains("ENC:", File.ReadAllText(path));
    }
}
