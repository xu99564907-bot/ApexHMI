#nullable enable
using ApexHMI.ViewModels;

namespace ApexHMI.Tests.ViewModels;

/// <summary>
/// M7.4: 测试专用 Shell — 通过 MainViewModel 的测试 ctor 绕开 WPF/Dispatcher/Seed 初始化。
/// 用于把 Module ViewModel（GitPull / Manual / Parameter / Alarm 等）以本实例为 Shell 单独实例化，
/// 验证命令绑定 / 委托 / 属性等"窄面"行为，无需启动整套 Application。
/// </summary>
internal sealed class TestShell : MainViewModel
{
    public TestShell() : base("test-only") { }
}
