using ApexHMI.ViewModels.Shell;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ApexHMI.Tests.ViewModels;

public class AlarmViewModelTests
{
    [Fact]
    public void AlarmModuleOwnsAlarmPageCommands()
    {
        using var provider = Bootstrapper.BuildServiceProvider();
        var shell = provider.GetRequiredService<MainWindowViewModel>();

        Assert.NotNull(shell.Alarm.AcknowledgeAllAlarmsCommand);
        Assert.NotNull(shell.Alarm.ResetAllAlarmsCommand);
        Assert.NotNull(shell.Alarm.SaveAlarmHistoryCommand);
        Assert.NotNull(shell.Alarm.LoadAlarmHistoryCommand);

        Assert.NotSame(shell.AcknowledgeAllAlarmsCommand, shell.Alarm.AcknowledgeAllAlarmsCommand);
        Assert.NotSame(shell.ResetAllAlarmsCommand, shell.Alarm.ResetAllAlarmsCommand);
        Assert.NotSame(shell.SaveAlarmHistoryCommand, shell.Alarm.SaveAlarmHistoryCommand);
        Assert.NotSame(shell.LoadAlarmHistoryCommand, shell.Alarm.LoadAlarmHistoryCommand);
    }
}
