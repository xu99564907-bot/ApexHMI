using ApexHMI.ViewModels.Modules;
using ApexHMI.ViewModels.Shell;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ApexHMI.Tests.ViewModels;

public class ManualViewModelTests {
    [Fact(Skip = "M6.4: 需要完整 WPF Application 集成测试基座 — 推迟到 M7 窄面重写")]
    public void ManualModuleOwnsAll17Commands()
    {
        using var provider = Bootstrapper.BuildServiceProvider();
        var shell = provider.GetRequiredService<MainWindowViewModel>();

        Assert.NotNull(shell.Manual.ToggleDeviceCommand);
        Assert.NotNull(shell.Manual.CylinderMoveToHomeCommand);
        Assert.NotNull(shell.Manual.CylinderMoveToWorkCommand);
        Assert.NotNull(shell.Manual.ToggleCylinderHomeMaskCommand);
        Assert.NotNull(shell.Manual.ToggleCylinderWorkMaskCommand);
        Assert.NotNull(shell.Manual.SetDebugModeCommand);
        Assert.NotNull(shell.Manual.SetDryRunModeCommand);
        Assert.NotNull(shell.Manual.SetBypassStationModeCommand);
        Assert.NotNull(shell.Manual.SetManualModeCommand);
        Assert.NotNull(shell.Manual.SetAutoModeCommand);
        Assert.NotNull(shell.Manual.StartDeviceCommand);
        Assert.NotNull(shell.Manual.StopDeviceCommand);
        Assert.NotNull(shell.Manual.ResetAlarmFromHomeCommand);
        Assert.NotNull(shell.Manual.ResetMotorFaultCommand);
        Assert.NotNull(shell.Manual.PauseRobotCommand);
        Assert.NotNull(shell.Manual.ResetRobotCommand);
        Assert.NotNull(shell.Manual.ToggleAxisEnableCommand);
        Assert.NotNull(shell.Manual.AxisAlarmResetCommand);
    }

    [Fact(Skip = "M6.4: 需要完整 WPF Application 集成测试基座 — 推迟到 M7 窄面重写")]
    public void ManualCommandsAreDistinctFromShell()
    {
        using var provider = Bootstrapper.BuildServiceProvider();
        var shell = provider.GetRequiredService<MainWindowViewModel>();

        Assert.NotSame(shell.ToggleDeviceCommand, shell.Manual.ToggleDeviceCommand);
        Assert.NotSame(shell.CylinderMoveToHomeCommand, shell.Manual.CylinderMoveToHomeCommand);
        Assert.NotSame(shell.CylinderMoveToWorkCommand, shell.Manual.CylinderMoveToWorkCommand);
        Assert.NotSame(shell.SetDebugModeCommand, shell.Manual.SetDebugModeCommand);
        Assert.NotSame(shell.SetDryRunModeCommand, shell.Manual.SetDryRunModeCommand);
        Assert.NotSame(shell.SetBypassStationModeCommand, shell.Manual.SetBypassStationModeCommand);
        Assert.NotSame(shell.SetManualModeCommand, shell.Manual.SetManualModeCommand);
        Assert.NotSame(shell.SetAutoModeCommand, shell.Manual.SetAutoModeCommand);
        Assert.NotSame(shell.StartDeviceCommand, shell.Manual.StartDeviceCommand);
        Assert.NotSame(shell.StopDeviceCommand, shell.Manual.StopDeviceCommand);
        Assert.NotSame(shell.ResetAlarmFromHomeCommand, shell.Manual.ResetAlarmFromHomeCommand);
        Assert.NotSame(shell.ResetMotorFaultCommand, shell.Manual.ResetMotorFaultCommand);
        Assert.NotSame(shell.PauseRobotCommand, shell.Manual.PauseRobotCommand);
        Assert.NotSame(shell.ResetRobotCommand, shell.Manual.ResetRobotCommand);
        Assert.NotSame(shell.ToggleAxisEnableCommand, shell.Manual.ToggleAxisEnableCommand);
        Assert.NotSame(shell.AxisAlarmResetCommand, shell.Manual.AxisAlarmResetCommand);
    }

    [Fact(Skip = "M6.4: 需要完整 WPF Application 集成测试基座 — 推迟到 M7 窄面重写")]
    public void ManualModuleDelegatesCollections()
    {
        using var provider = Bootstrapper.BuildServiceProvider();
        var shell = provider.GetRequiredService<MainWindowViewModel>();

        Assert.Same(shell.ManualCylinderBlocks, shell.Manual.CylinderBlocks);
        Assert.Same(shell.ManualAxisBlocks, shell.Manual.AxisBlocks);
    }

    [Fact(Skip = "M6.4: 需要完整 WPF Application 集成测试基座 — 推迟到 M7 窄面重写")]
    public void ManualModuleDelegatesCylinderProperties()
    {
        using var provider = Bootstrapper.BuildServiceProvider();
        var shell = provider.GetRequiredService<MainWindowViewModel>();

        // Read delegation
        Assert.Equal(shell.CylinderHomeMaskEnabled, shell.Manual.CylinderHomeMaskEnabled);
        Assert.Equal(shell.CylinderWorkMaskEnabled, shell.Manual.CylinderWorkMaskEnabled);

        // Write delegation
        shell.Manual.CylinderHomeMaskEnabled = true;
        Assert.True(shell.CylinderHomeMaskEnabled);

        shell.Manual.AxisJogDistance = "10.0";
        Assert.Equal("10.0", shell.AxisJogDistance);

        shell.Manual.ManualWriteTagName = "Test.Tag";
        Assert.Equal("Test.Tag", shell.ManualWriteTagName);
    }
}
