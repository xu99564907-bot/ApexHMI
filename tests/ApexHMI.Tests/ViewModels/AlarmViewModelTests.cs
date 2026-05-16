#nullable enable
using ApexHMI.Interfaces;
using ApexHMI.ViewModels.Modules;
using Moq;
using Xunit;

namespace ApexHMI.Tests.ViewModels;

/// <summary>
/// M7.4: Moq + TestShell 重写。验证 Alarm 模块持有的命令独立于 Shell 命令实例。
/// </summary>
public class AlarmViewModelTests
{
    [Fact]
    public void AlarmModuleOwnsAlarmPageCommands()
    {
        var shell = new TestShell();
        var alarmSvc = new Mock<IAlarmService>(MockBehavior.Loose).Object;
        var alarm = new AlarmViewModel(shell, alarmSvc);

        Assert.NotNull(alarm.AcknowledgeAllAlarmsCommand);
        Assert.NotNull(alarm.ResetAllAlarmsCommand);
        Assert.NotNull(alarm.SaveAlarmHistoryCommand);
        Assert.NotNull(alarm.LoadAlarmHistoryCommand);

        Assert.NotSame(shell.AcknowledgeAllAlarmsCommand, alarm.AcknowledgeAllAlarmsCommand);
        Assert.NotSame(shell.ResetAllAlarmsCommand, alarm.ResetAllAlarmsCommand);
        Assert.NotSame(shell.SaveAlarmHistoryCommand, alarm.SaveAlarmHistoryCommand);
        Assert.NotSame(shell.LoadAlarmHistoryCommand, alarm.LoadAlarmHistoryCommand);
    }
}
