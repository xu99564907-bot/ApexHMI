#nullable enable
using ApexHMI.Interfaces;
using ApexHMI.ViewModels.Modules;
using Moq;
using Xunit;

namespace ApexHMI.Tests.ViewModels;

/// <summary>
/// M7.4: Moq + TestShell 重写。验证 Parameter 模块命令独立于 Shell 命令。
/// </summary>
public class ParameterViewModelTests
{
    [Fact]
    public void ParameterModuleOwnsParameterPersistenceCommands()
    {
        var shell = new TestShell();
        var paramSvc = new Mock<IParameterService>(MockBehavior.Loose).Object;
        var parameters = new ParameterViewModel(shell, paramSvc);

        Assert.NotNull(parameters.LoadParametersCommand);
        Assert.NotNull(parameters.SaveParametersCommand);
        Assert.NotSame(shell.LoadParametersCommand, parameters.LoadParametersCommand);
        Assert.NotSame(shell.SaveParametersCommand, parameters.SaveParametersCommand);
    }
}
