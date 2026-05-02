using ApexHMI.Tests.TestHelpers;
using ApexHMI.Services.OpcUa;
using Opc.Ua;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace ApexHMI.Tests.Services;

public class OpcUaApplicationConfigurationFactoryTests
{
    [Fact]
    public async Task BuildAsyncCreatesClientConfigurationAndPkiStoresUnderApplicationRoot()
    {
        using var tempDir = TempDir.Create();
        var appRoot = tempDir.Path;

        var configuration = await OpcUaApplicationConfigurationFactory.BuildAsync(appRoot);

        Assert.Equal("ApexHMI", configuration.ApplicationName);
        Assert.Equal(ApplicationType.Client, configuration.ApplicationType);
        Assert.True(configuration.SecurityConfiguration.AutoAcceptUntrustedCertificates);
        Assert.Equal(
            Path.Combine(appRoot, "config", "pki", "own"),
            configuration.SecurityConfiguration.ApplicationCertificate.StorePath);
        Assert.True(Directory.Exists(Path.Combine(appRoot, "config", "pki", "own")));
        Assert.True(Directory.Exists(Path.Combine(appRoot, "config", "pki", "trusted")));
        Assert.True(Directory.Exists(Path.Combine(appRoot, "config", "pki", "issuer")));
        Assert.True(Directory.Exists(Path.Combine(appRoot, "config", "pki", "rejected")));
    }
}
