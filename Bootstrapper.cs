using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ApexHMI.Interfaces;
using ApexHMI.Models;
using ApexHMI.Services;
using ApexHMI.Services.DataBinding;
using ApexHMI.Services.RuntimeUi;
using ApexHMI.ViewModels.Shell;
using ApexHMI.Views;
using Microsoft.Extensions.Options;
using Serilog;

namespace ApexHMI;

public static class Bootstrapper
{
    public static ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();

        services.AddLogging(b =>
        {
            b.ClearProviders();
            b.AddSerilog(Log.Logger, dispose: false);
        });

        // Localization (ResX-based, zh-CN / en-US)
        var localizationService = new LocalizationService();
        services.AddSingleton<ILocalizationService>(localizationService);
        services.AddSingleton(localizationService);
        Converters.LocExtension.Initialize(localizationService);

        var appOptions = new AppOptions();
        services.AddSingleton<IOptions<AppOptions>>(Options.Create(appOptions));
        services.AddSingleton<IOptions<ConfigFileOptions>>(Options.Create(appOptions.ConfigFiles));
        services.AddSingleton<IOptions<OpcUaRuntimeOptions>>(Options.Create(appOptions.OpcUa));
        services.AddSingleton<IOptions<IoProgramGenerationOptions>>(Options.Create(appOptions.IoProgramGeneration));

        services.AddSingleton<IOpcUaService, OpcUaService>();
        services.AddSingleton<IConfigurationService, ConfigurationService>();
        services.AddSingleton<IParameterService, ParameterService>();
        services.AddSingleton<IAlarmService, AlarmService>();
        services.AddSingleton<IRecipeService, RecipeService>();

        services.AddSingleton<ICsvImportService, CsvImportService>();
        services.AddSingleton(sp => (CsvImportService)sp.GetRequiredService<ICsvImportService>());
        services.AddSingleton<IXmlImportService, XmlImportService>();
        services.AddSingleton(sp => (XmlImportService)sp.GetRequiredService<IXmlImportService>());
        services.AddSingleton<IIoTableParser, IoTableParser>();
        services.AddSingleton<IIoTableImportService, IoTableImportService>();
        services.AddSingleton(sp => (IoTableImportService)sp.GetRequiredService<IIoTableImportService>());
        services.AddSingleton<IIoProgramGenerationService, IoProgramGenerationService>();
        services.AddSingleton(sp => (IoProgramGenerationService)sp.GetRequiredService<IIoProgramGenerationService>());
        services.AddSingleton<IGitPullService, GitPullService>();
        services.AddSingleton(sp => (GitPullService)sp.GetRequiredService<IGitPullService>());
        services.AddSingleton<IGeneratedArtifactSyncService, GeneratedArtifactSyncService>();
        services.AddSingleton(sp => (GeneratedArtifactSyncService)sp.GetRequiredService<IGeneratedArtifactSyncService>());
        services.AddSingleton<INamingRulesService, NamingRulesService>();
        services.AddSingleton(sp => (NamingRulesService)sp.GetRequiredService<INamingRulesService>());
        services.AddSingleton<IFlowLogCsvService, FlowLogCsvService>();
        services.AddSingleton(sp => (FlowLogCsvService)sp.GetRequiredService<IFlowLogCsvService>());
        services.AddSingleton<ITrendHistoryService, TrendHistoryService>();
        services.AddSingleton(sp => (TrendHistoryService)sp.GetRequiredService<ITrendHistoryService>());

        // 开放平台 — 动态页面运行时
        services.AddSingleton<IDataPointCatalog, DataPointCatalog>();
        services.AddSingleton<IWidgetViewFactory, WidgetRegistry>();
        services.AddSingleton<RuntimeProjectService>();
        services.AddSingleton<RuntimeDataBindingService>();

        // 开放平台 — 编辑器服务 (Phase B)
        services.AddSingleton<IProjectEditorService, ProjectEditorService>();
        services.AddSingleton<IWidgetEditorService, WidgetEditorService>();
        services.AddSingleton<WidgetBlockGenerator>();
        services.AddSingleton<ManualPageAutoGenerator>();

        services.AddSingleton<RefreshCoordinator>();

        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainWindow>();

        return services.BuildServiceProvider();
    }
}
