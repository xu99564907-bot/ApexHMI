#nullable enable
using System;
using System.Windows;
using LibVLCSharp.Shared;
using LibVLCSharp.WPF;
using ApexHMI.Models.RuntimeUi;
using ApexHMI.Services.RuntimeUi;

namespace ApexHMI.ViewModels.Runtime;

/// <summary>
/// P9D: 摄像头视图（RTSP / MJPEG / 本地视频文件，基于 LibVLCSharp）。
/// 设计时显示占位文本，不连流；运行时由 View Loaded 触发 StartPlayback。
/// </summary>
public partial class CameraViewWidgetViewModel : WidgetViewModelBase
{
    private static LibVLC? _sharedVlc;
    private MediaPlayer? _player;

    public CameraViewWidgetViewModel(WidgetInstance model, IWidgetDataContext dataContext)
        : base(model, dataContext)
    {
    }

    public string Url           => Prop("url", "");
    public string Username      => Prop("username", "");
    public string Password      => Prop("password", "");
    public bool   AutoReconnect => string.Equals(Prop("autoReconnect", "true"), "true", StringComparison.OrdinalIgnoreCase);
    public string Background    => Prop("background", "#000000");

    public bool IsDesignTimeView
    {
        get
        {
            var shell = _dataContext.Shell;
            if (shell is null) return true;
            return shell.GetType().Name.Contains("Designer", StringComparison.OrdinalIgnoreCase);
        }
    }

    public Visibility DesignPlaceholderVisibility => IsDesignTimeView ? Visibility.Visible : Visibility.Collapsed;
    public Visibility RuntimeViewVisibility        => IsDesignTimeView ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>由 View.Loaded 调用：初始化 LibVLC 并开始播放。</summary>
    public void StartPlayback(VideoView host)
    {
        if (string.IsNullOrWhiteSpace(Url)) return;

        // LibVLC native 初始化（每进程一次）
        if (_sharedVlc is null)
        {
            try
            {
                Core.Initialize();
                _sharedVlc = new LibVLC();
            }
            catch
            {
                // native dll 加载失败 — 静默
                return;
            }
        }

        _player = new MediaPlayer(_sharedVlc);
        host.MediaPlayer = _player;

        var url = ComposeUrl();
        using var media = new Media(_sharedVlc, new Uri(url));
        _player.Play(media);
    }

    public void StopPlayback()
    {
        try
        {
            _player?.Stop();
            _player?.Dispose();
            _player = null;
        }
        catch { /* ignore */ }
    }

    /// <summary>把 username/password 注入 URL（仅当 URL 不含 @ 时）。</summary>
    private string ComposeUrl()
    {
        var url = Url;
        if (string.IsNullOrEmpty(Username) || url.Contains('@')) return url;
        try
        {
            var u = new Uri(url);
            var auth = string.IsNullOrEmpty(Password) ? Username : $"{Username}:{Password}";
            return $"{u.Scheme}://{auth}@{u.Authority}{u.PathAndQuery}";
        }
        catch { return url; }
    }
}
