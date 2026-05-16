#nullable enable
using System;
using System.Globalization;
using System.IO;
using System.Windows;
using ApexHMI.Models.RuntimeUi;
using ApexHMI.Services.RuntimeUi;

namespace ApexHMI.ViewModels.Runtime;

/// <summary>
/// P9C: 媒体播放器（WPF 内置 MediaElement，本地 mp4/mp3 + http(s) 流）。
/// </summary>
public partial class MediaPlayerWidgetViewModel : WidgetViewModelBase
{
    public MediaPlayerWidgetViewModel(WidgetInstance model, IWidgetDataContext dataContext)
        : base(model, dataContext)
    {
    }

    public string Source     => Prop("source", "");
    public bool   AutoPlay   => string.Equals(Prop("autoPlay", "false"), "true", StringComparison.OrdinalIgnoreCase);
    public bool   Loop       => string.Equals(Prop("loop", "false"), "true", StringComparison.OrdinalIgnoreCase);
    public bool   ShowToolbar=> string.Equals(Prop("showToolbar", "true"), "true", StringComparison.OrdinalIgnoreCase);

    public double Volume
    {
        get
        {
            var raw = Prop("volume", "0.5");
            if (!double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var v)) v = 0.5;
            return Math.Max(0.0, Math.Min(1.0, v));
        }
    }

    public Uri? MediaUri
    {
        get
        {
            var s = Source;
            if (string.IsNullOrWhiteSpace(s)) return null;
            try
            {
                if (Uri.TryCreate(s, UriKind.Absolute, out var direct)) return direct;
                if (File.Exists(s)) return new Uri(Path.GetFullPath(s), UriKind.Absolute);
            }
            catch { /* ignore */ }
            return null;
        }
    }

    public Visibility ToolbarVisibility           => ShowToolbar ? Visibility.Visible : Visibility.Collapsed;
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
}
