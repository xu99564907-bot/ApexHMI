#nullable enable
using ApexHMI.ViewModels.Modules;
using Xunit;

namespace ApexHMI.Tests.ViewModels;

/// <summary>M7.4: Moq + TestShell 重写。验证 Monitor 模块命令独立于 Shell 命令。</summary>
public class MonitorViewModelTests
{
    [Fact]
    public void MonitorModuleOwnsMonitorPageCommands()
    {
        var shell = new TestShell();
        var monitor = new MonitorViewModel(shell);

        Assert.NotNull(monitor.RefreshTagsCommand);
        Assert.NotNull(monitor.LoadTrendHistoryCommand);
        Assert.NotNull(monitor.ImportFlowCsvCommand);
        Assert.NotNull(monitor.LoadOpcUaBrowserRootCommand);
        Assert.NotNull(monitor.SwitchIoMonitorTypeCommand);
        Assert.NotNull(monitor.PauseProgramMonitorTraceCommand);
        Assert.NotNull(monitor.ExportProgramMonitorTraceCsvCommand);

        Assert.NotSame(shell.RefreshTagsCommand, monitor.RefreshTagsCommand);
        Assert.NotSame(shell.LoadTrendHistoryCommand, monitor.LoadTrendHistoryCommand);
        Assert.NotSame(shell.ImportFlowCsvCommand, monitor.ImportFlowCsvCommand);
        Assert.NotSame(shell.LoadOpcUaBrowserRootCommand, monitor.LoadOpcUaBrowserRootCommand);
        Assert.NotSame(shell.SwitchIoMonitorTypeCommand, monitor.SwitchIoMonitorTypeCommand);
        Assert.NotSame(shell.PauseProgramMonitorTraceCommand, monitor.PauseProgramMonitorTraceCommand);
        Assert.NotSame(shell.ExportProgramMonitorTraceCsvCommand, monitor.ExportProgramMonitorTraceCsvCommand);
    }
}
