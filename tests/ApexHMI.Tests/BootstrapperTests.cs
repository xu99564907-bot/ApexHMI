using ApexHMI.Interfaces;
using ApexHMI.Models;
using ApexHMI.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace ApexHMI.Tests;

public class BootstrapperTests
{
    [Fact]
    public void BuildServiceProviderRegistersServiceInterfacesAndConcreteCompatibilityTypes()
    {
        using var provider = Bootstrapper.BuildServiceProvider();

        AssertCompatibility<ICsvImportService, CsvImportService>(provider);
        AssertCompatibility<IXmlImportService, XmlImportService>(provider);
        AssertCompatibility<IIoTableImportService, IoTableImportService>(provider);
        AssertCompatibility<IIoProgramGenerationService, IoProgramGenerationService>(provider);
        AssertCompatibility<IGitPullService, GitPullService>(provider);
        AssertCompatibility<IGeneratedArtifactSyncService, GeneratedArtifactSyncService>(provider);
        AssertCompatibility<INamingRulesService, NamingRulesService>(provider);
        AssertCompatibility<IDesignerLayoutService, DesignerLayoutService>(provider);
        AssertCompatibility<IDesignerProjectService, DesignerProjectService>(provider);
        AssertCompatibility<IFlowLogCsvService, FlowLogCsvService>(provider);
        AssertCompatibility<ITrendHistoryService, TrendHistoryService>(provider);
    }

    [Fact]
    public void BuildServiceProviderRegistersApplicationOptions()
    {
        using var provider = Bootstrapper.BuildServiceProvider();

        Assert.Equal("config", provider.GetRequiredService<IOptions<ConfigFileOptions>>().Value.ConfigDirectoryName);
        Assert.Equal("appsettings.json", provider.GetRequiredService<IOptions<ConfigFileOptions>>().Value.AppSettingsFileName);
        Assert.Equal(15000, provider.GetRequiredService<IOptions<OpcUaRuntimeOptions>>().Value.ConnectAttemptTimeoutMs);
        Assert.Equal("192.168.0.10", provider.GetRequiredService<IOptions<IoProgramGenerationOptions>>().Value.EpsonRobotIp);
        Assert.Same(
            provider.GetRequiredService<IOptions<AppOptions>>().Value.ConfigFiles,
            provider.GetRequiredService<IOptions<ConfigFileOptions>>().Value);
    }

    private static void AssertCompatibility<TInterface, TConcrete>(IServiceProvider provider)
        where TInterface : class
        where TConcrete : class, TInterface
    {
        var byInterface = provider.GetRequiredService<TInterface>();
        var byConcrete = provider.GetRequiredService<TConcrete>();

        Assert.Same(byInterface, byConcrete);
    }
}
