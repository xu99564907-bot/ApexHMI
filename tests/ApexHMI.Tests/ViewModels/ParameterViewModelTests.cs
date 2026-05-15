using ApexHMI.ViewModels.Shell;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ApexHMI.Tests.ViewModels;

public class ParameterViewModelTests {
    [Fact(Skip = "M6.4: 需要完整 WPF Application 集成测试基座 — 推迟到 M7 窄面重写")]
    public void ParameterModuleOwnsParameterPersistenceCommands()
    {
        using var provider = Bootstrapper.BuildServiceProvider();
        var shell = provider.GetRequiredService<MainWindowViewModel>();

        Assert.NotNull(shell.ParametersModule.LoadParametersCommand);
        Assert.NotNull(shell.ParametersModule.SaveParametersCommand);
        Assert.NotSame(shell.LoadParametersCommand, shell.ParametersModule.LoadParametersCommand);
        Assert.NotSame(shell.SaveParametersCommand, shell.ParametersModule.SaveParametersCommand);
    }
}
