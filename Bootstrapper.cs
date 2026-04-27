using Microsoft.Extensions.DependencyInjection;
using ApexHMI.Interfaces;
using ApexHMI.Services;
using ApexHMI.ViewModels.Shell;
using ApexHMI.Views;

namespace ApexHMI;

public static class Bootstrapper
{
    public static ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IOpcUaService, OpcUaService>();
        services.AddSingleton<IConfigurationService, ConfigurationService>();
        services.AddSingleton<IParameterService, ParameterService>();
        services.AddSingleton<IAlarmService, AlarmService>();
        services.AddSingleton<IRecipeService, RecipeService>();

        services.AddSingleton<CsvImportService>();
        services.AddSingleton<XmlImportService>();
        services.AddSingleton<IoTableImportService>();
        services.AddSingleton<IoProgramGenerationService>();
        services.AddSingleton<GitPullService>();
        services.AddSingleton<GeneratedArtifactSyncService>();
        services.AddSingleton<NamingRulesService>();
        services.AddSingleton<DesignerLayoutService>();
        services.AddSingleton<DesignerProjectService>();
        services.AddSingleton<FlowLogCsvService>();
        services.AddSingleton<TrendHistoryService>();

        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainWindow>();

        return services.BuildServiceProvider();
    }
}
