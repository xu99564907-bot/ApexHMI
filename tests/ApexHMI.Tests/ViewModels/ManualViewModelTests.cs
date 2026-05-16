#nullable enable
using ApexHMI.ViewModels.Modules;
using Xunit;

namespace ApexHMI.Tests.ViewModels;

/// <summary>
/// M7.4: Moq + TestShell 重写 — 不再走 Bootstrapper/WPF Application。
/// 测试范围：Manual 模块的命令存在 / 与 Shell 命令的实例区别 / 集合委托 / 属性委托。
/// </summary>
public class ManualViewModelTests
{
    private static (TestShell shell, ManualViewModel manual) CreateSut()
    {
        var shell = new TestShell();
        var manual = new ManualViewModel(shell);
        return (shell, manual);
    }

    [Fact]
    public void ManualModuleOwnsAll17Commands()
    {
        var (_, manual) = CreateSut();
        Assert.NotNull(manual.ToggleDeviceCommand);
        Assert.NotNull(manual.CylinderMoveToHomeCommand);
        Assert.NotNull(manual.CylinderMoveToWorkCommand);
        Assert.NotNull(manual.ToggleCylinderHomeMaskCommand);
        Assert.NotNull(manual.ToggleCylinderWorkMaskCommand);
        Assert.NotNull(manual.SetDebugModeCommand);
        Assert.NotNull(manual.SetDryRunModeCommand);
        Assert.NotNull(manual.SetBypassStationModeCommand);
        Assert.NotNull(manual.SetManualModeCommand);
        Assert.NotNull(manual.SetAutoModeCommand);
        Assert.NotNull(manual.StartDeviceCommand);
        Assert.NotNull(manual.StopDeviceCommand);
        Assert.NotNull(manual.ResetAlarmFromHomeCommand);
        Assert.NotNull(manual.ResetMotorFaultCommand);
        Assert.NotNull(manual.PauseRobotCommand);
        Assert.NotNull(manual.ResetRobotCommand);
        Assert.NotNull(manual.ToggleAxisEnableCommand);
        Assert.NotNull(manual.AxisAlarmResetCommand);
    }

    [Fact]
    public void ManualCommandsAreDistinctFromShell()
    {
        var (shell, manual) = CreateSut();
        Assert.NotSame(shell.ToggleDeviceCommand, manual.ToggleDeviceCommand);
        Assert.NotSame(shell.CylinderMoveToHomeCommand, manual.CylinderMoveToHomeCommand);
        Assert.NotSame(shell.CylinderMoveToWorkCommand, manual.CylinderMoveToWorkCommand);
        Assert.NotSame(shell.SetDebugModeCommand, manual.SetDebugModeCommand);
        Assert.NotSame(shell.SetDryRunModeCommand, manual.SetDryRunModeCommand);
        Assert.NotSame(shell.SetBypassStationModeCommand, manual.SetBypassStationModeCommand);
        Assert.NotSame(shell.SetManualModeCommand, manual.SetManualModeCommand);
        Assert.NotSame(shell.SetAutoModeCommand, manual.SetAutoModeCommand);
        Assert.NotSame(shell.StartDeviceCommand, manual.StartDeviceCommand);
        Assert.NotSame(shell.StopDeviceCommand, manual.StopDeviceCommand);
        Assert.NotSame(shell.ResetAlarmFromHomeCommand, manual.ResetAlarmFromHomeCommand);
        Assert.NotSame(shell.ResetMotorFaultCommand, manual.ResetMotorFaultCommand);
        Assert.NotSame(shell.PauseRobotCommand, manual.PauseRobotCommand);
        Assert.NotSame(shell.ResetRobotCommand, manual.ResetRobotCommand);
        Assert.NotSame(shell.ToggleAxisEnableCommand, manual.ToggleAxisEnableCommand);
        Assert.NotSame(shell.AxisAlarmResetCommand, manual.AxisAlarmResetCommand);
    }

    [Fact]
    public void ManualModuleDelegatesCollections()
    {
        var (shell, manual) = CreateSut();
        Assert.Same(shell.ManualCylinderBlocks, manual.CylinderBlocks);
        Assert.Same(shell.ManualAxisBlocks, manual.AxisBlocks);
    }

    [Fact]
    public void ManualModuleDelegatesCylinderProperties()
    {
        var (shell, manual) = CreateSut();
        Assert.Equal(shell.CylinderHomeMaskEnabled, manual.CylinderHomeMaskEnabled);
        Assert.Equal(shell.CylinderWorkMaskEnabled, manual.CylinderWorkMaskEnabled);
        manual.CylinderHomeMaskEnabled = true;
        Assert.True(shell.CylinderHomeMaskEnabled);
        manual.AxisJogDistance = "10.0";
        Assert.Equal("10.0", shell.AxisJogDistance);
        manual.ManualWriteTagName = "Test.Tag";
        Assert.Equal("Test.Tag", shell.ManualWriteTagName);
    }
}
