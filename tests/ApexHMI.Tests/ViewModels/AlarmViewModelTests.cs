using ApexHMI.ViewModels.Shell;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ApexHMI.Tests.ViewModels;

public class AlarmViewModelTests
{
    // M6.4: Bootstrapper.BuildServiceProvider().GetRequiredService<MainWindowViewModel>()
    // 会构造完整的 WPF DesignerEditorViewModel 树（含 TextWidget 等 UserControl + StaticResource），
    // 在单元测试 testhost 中需要 STA 线程 + WPF Application + 完整资源字典；
    // 尝试 Xunit.StaFact + 单一 STA Dispatcher 后仍会触发 cross-thread CollectionView 异常或 testhost 挂死。
    // 此类测试本质上是 module 间命令委托 contract 测试，应改为窄面 ViewModel 单测（不构造整个 Shell）
    // 或迁到 UI 集成测试套件。M6.4 决定先 skip + 注释；M7 再窄化重写。
    [Fact(Skip = "M6.4: 需要完整 WPF Application 集成测试基座 — 推迟到 M7 窄面重写")]
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
