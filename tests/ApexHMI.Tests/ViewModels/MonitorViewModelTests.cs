using ApexHMI.ViewModels.Shell;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ApexHMI.Tests.ViewModels;

public class MonitorViewModelTests
{
    [Fact]
    public void MonitorModuleOwnsMonitorPageCommands()
    {
        using var provider = Bootstrapper.BuildServiceProvider();
        var shell = provider.GetRequiredService<MainWindowViewModel>();

        Assert.NotNull(shell.Monitor.RefreshTagsCommand);
        Assert.NotNull(shell.Monitor.LoadTrendHistoryCommand);
        Assert.NotNull(shell.Monitor.ImportFlowCsvCommand);
        Assert.NotNull(shell.Monitor.LoadOpcUaBrowserRootCommand);
        Assert.NotNull(shell.Monitor.SwitchIoMonitorTypeCommand);
        Assert.NotNull(shell.Monitor.PauseProgramMonitorTraceCommand);
        Assert.NotNull(shell.Monitor.ExportProgramMonitorTraceCsvCommand);

        Assert.NotSame(shell.RefreshTagsCommand, shell.Monitor.RefreshTagsCommand);
        Assert.NotSame(shell.LoadTrendHistoryCommand, shell.Monitor.LoadTrendHistoryCommand);
        Assert.NotSame(shell.ImportFlowCsvCommand, shell.Monitor.ImportFlowCsvCommand);
        Assert.NotSame(shell.LoadOpcUaBrowserRootCommand, shell.Monitor.LoadOpcUaBrowserRootCommand);
        Assert.NotSame(shell.SwitchIoMonitorTypeCommand, shell.Monitor.SwitchIoMonitorTypeCommand);
        Assert.NotSame(shell.PauseProgramMonitorTraceCommand, shell.Monitor.PauseProgramMonitorTraceCommand);
        Assert.NotSame(shell.ExportProgramMonitorTraceCsvCommand, shell.Monitor.ExportProgramMonitorTraceCsvCommand);
    }
}
