using System;
using System.IO;
using System.Threading.Tasks;
using ApexHMI.Interfaces;
using ApexHMI.Services.Security;
using FluentAssertions;
using Moq;
using Xunit;

namespace ApexHMI.Tests.Services;

/// <summary>
/// M7.1: AccountLockoutService SQLite 持久化测试。
/// 核心场景：进程"重启"（销毁实例再用同 data dir 新建）后，锁定状态仍生效。
/// </summary>
public sealed class AccountLockoutPersistenceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly Mock<IAuditService> _audit;

    public AccountLockoutPersistenceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ApexHMI.LockoutTests." + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _audit = new Mock<IAuditService>();
        _audit.Setup(a => a.LogOperationAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<bool>(), It.IsAny<string?>())).Returns(Task.CompletedTask);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true); } catch { /* ignore */ }
    }

    [Fact]
    public void Lockout_Survives_Restart()
    {
        // 第 1 个实例：触发锁定
        var svc1 = new AccountLockoutService(_audit.Object, _tempDir)
        {
            MaxAttempts = 3,
            LockoutDuration = TimeSpan.FromMinutes(30),
        };
        for (var i = 0; i < 3; i++) svc1.RegisterFailure("alice");
        svc1.IsLocked("alice").Should().BeTrue();

        // 第 2 个实例：模拟进程重启，加载同目录数据
        var svc2 = new AccountLockoutService(_audit.Object, _tempDir);
        svc2.IsLocked("alice").Should().BeTrue("重启后锁定状态应从 SQLite 恢复");
        svc2.GetLockedUntil("alice").Should().NotBeNull();
    }

    [Fact]
    public void Expired_Lock_Is_Cleared_On_Load()
    {
        var svc1 = new AccountLockoutService(_audit.Object, _tempDir)
        {
            MaxAttempts = 1,
            LockoutDuration = TimeSpan.FromMilliseconds(50),
        };
        svc1.RegisterFailure("bob");
        svc1.IsLocked("bob").Should().BeTrue();
        System.Threading.Thread.Sleep(120);

        // 重启 → 加载时应清掉过期记录
        var svc2 = new AccountLockoutService(_audit.Object, _tempDir);
        svc2.IsLocked("bob").Should().BeFalse();
        svc2.GetLockedUntil("bob").Should().BeNull();
    }

    [Fact]
    public void Reset_Clears_Db()
    {
        var svc1 = new AccountLockoutService(_audit.Object, _tempDir) { MaxAttempts = 3 };
        svc1.RegisterFailure("carol");
        svc1.RegisterFailure("carol");
        svc1.ResetCounter("carol");

        var svc2 = new AccountLockoutService(_audit.Object, _tempDir);
        svc2.GetFailureCount("carol").Should().Be(0);
        svc2.IsLocked("carol").Should().BeFalse();
    }

    [Fact]
    public async Task Admin_Unlock_Persists()
    {
        var svc1 = new AccountLockoutService(_audit.Object, _tempDir)
        {
            MaxAttempts = 2,
            LockoutDuration = TimeSpan.FromHours(1),
        };
        svc1.RegisterFailure("dave");
        svc1.RegisterFailure("dave");
        svc1.IsLocked("dave").Should().BeTrue();
        await svc1.UnlockAsync("dave", "admin");

        var svc2 = new AccountLockoutService(_audit.Object, _tempDir);
        svc2.IsLocked("dave").Should().BeFalse();
    }
}
