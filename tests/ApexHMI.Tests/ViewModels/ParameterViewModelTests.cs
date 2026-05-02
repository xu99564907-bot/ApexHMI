using ApexHMI.ViewModels.Shell;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ApexHMI.Tests.ViewModels;

public class ParameterViewModelTests
{
    [Fact]
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
