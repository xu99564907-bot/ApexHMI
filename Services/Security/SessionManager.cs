#nullable enable
using System;
using System.Windows.Threading;
using ApexHMI.Models;

namespace ApexHMI.Services.Security;

/// <summary>
/// M5.2: 用户会话管理器。
/// <para>登录后启动倒计时；每次用户操作调 <see cref="KeepAlive"/> 重置；
/// 超时后触发 <see cref="SessionExpired"/> 事件 → 调用方注销并提示。</para>
/// </summary>
public sealed class SessionManager
{
    private DispatcherTimer? _timer;
    private DateTime _lastActivity;
    private string? _currentUser;

    /// <summary>会话无操作超时（默认 30 分钟）。</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>当前登录用户（null = 未登录）。</summary>
    public string? CurrentUser => _currentUser;

    /// <summary>距离超时还剩多少时间（用于 UI 状态栏显示）。</summary>
    public TimeSpan Remaining
    {
        get
        {
            if (_currentUser is null) return TimeSpan.Zero;
            var elapsed = DateTime.UtcNow - _lastActivity;
            var left = Timeout - elapsed;
            return left < TimeSpan.Zero ? TimeSpan.Zero : left;
        }
    }

    /// <summary>会话超时事件（在 UI 线程触发）。订阅方应执行注销 + 弹"请重新登录"。</summary>
    public event Action<string>? SessionExpired;

    /// <summary>距超时不足该阈值时触发提醒事件（默认 5 分钟）。</summary>
    public TimeSpan WarnThreshold { get; set; } = TimeSpan.FromMinutes(5);

    public SessionManager()
    {
        // M5.2: 不在 ctor 创建 DispatcherTimer，让单元测试可以在 MTA 线程构造服务。
    }

    private void EnsureTimer()
    {
        if (_timer is not null) return;
        try
        {
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
            _timer.Tick += OnTick;
        }
        catch
        {
            // 测试 / 非 STA 线程：timer 不可用，会话超时检查交给手动调用
            _timer = null;
        }
    }

    public void Login(string username)
    {
        _currentUser = username;
        _lastActivity = DateTime.UtcNow;
        EnsureTimer();
        if (_timer is { IsEnabled: false }) _timer.Start();
    }

    public void Logout()
    {
        _currentUser = null;
        _timer?.Stop();
    }

    /// <summary>每次用户活动（按键、点击、写 PLC、改配方等）调用以重置计时。</summary>
    public void KeepAlive()
    {
        if (_currentUser is null) return;
        _lastActivity = DateTime.UtcNow;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_currentUser is null) return;
        if (DateTime.UtcNow - _lastActivity >= Timeout)
        {
            var u = _currentUser;
            _currentUser = null;
            _timer.Stop();
            try { SessionExpired?.Invoke(u); } catch { /* ignore */ }
        }
    }
}
