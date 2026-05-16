#nullable enable
using System;
using System.Globalization;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using ApexHMI.Models.RuntimeUi;
using ApexHMI.Services.RuntimeUi;

namespace ApexHMI.ViewModels.Runtime;

/// <summary>
/// 时钟（Clock）：digital 模式用 TextBlock + 1s Timer 刷新；analog 模式留作 TODO，第一版只做 digital。
/// </summary>
public partial class ClockWidgetViewModel : WidgetViewModelBase
{
    [ObservableProperty] private string _displayText = string.Empty;
    private DispatcherTimer? _timer;

    public ClockWidgetViewModel(WidgetInstance model, IWidgetDataContext dataContext)
        : base(model, dataContext)
    {
        StartTimer();
        Tick(null, EventArgs.Empty);
    }

    public string ModeProp        => Prop("mode",       "digital");
    public string Format          => Prop("format",     "yyyy-MM-dd HH:mm:ss");
    public string Foreground      => Prop("foreground", "#0F172A");
    public string Background      => Prop("background", "#FFFFFF");
    public string FontSizeRaw     => Prop("fontSize",   "14");
    public string ShowSecondsRaw  => Prop("analogShowSeconds", "true");

    // B3.2: WinCC Clock 扩展
    public bool ShowSeconds  => string.Equals(Prop("showSeconds", "true"), "true", StringComparison.OrdinalIgnoreCase);
    public bool ShowDate     => string.Equals(Prop("showDate", "true"), "true", StringComparison.OrdinalIgnoreCase);
    public bool Use24Hour    => string.Equals(Prop("use24Hour", "true"), "true", StringComparison.OrdinalIgnoreCase);
    public string TimeZoneId => Prop("timeZone", "");

    public bool IsDigital => string.Equals(ModeProp, "digital", System.StringComparison.OrdinalIgnoreCase);

    private void StartTimer()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += Tick;
        _timer.Start();
    }

    private void Tick(object? sender, EventArgs e)
    {
        var now = ResolveNow();
        var fmt = EffectiveFormat();
        try { DisplayText = now.ToString(fmt, CultureInfo.InvariantCulture); }
        catch (FormatException) { DisplayText = now.ToString(CultureInfo.InvariantCulture); }
    }

    /// <summary>B3.2: 按 timeZone / synchronizationMode 解析 now。NTP 模式留 TODO，目前按本地时钟。</summary>
    private DateTime ResolveNow()
    {
        var tz = TimeZoneId;
        if (string.IsNullOrWhiteSpace(tz)) return DateTime.Now;
        try
        {
            var info = TimeZoneInfo.FindSystemTimeZoneById(tz);
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, info);
        }
        catch
        {
            return DateTime.Now;
        }
    }

    /// <summary>B3.2: showSeconds=false 时剥离 ":ss" / "ss" / 12h 制时转 hh tt。</summary>
    private string EffectiveFormat()
    {
        var fmt = Format ?? "yyyy-MM-dd HH:mm:ss";
        if (!ShowSeconds)
        {
            // 去掉 :ss 与 ss
            fmt = System.Text.RegularExpressions.Regex.Replace(fmt, @"[:\.]?ss", string.Empty);
        }
        if (!ShowDate)
        {
            // 去掉日期段（最常见 yyyy-MM-dd / yyyy/MM/dd / yyyy.MM.dd 前缀）
            fmt = System.Text.RegularExpressions.Regex.Replace(fmt, @"^\s*y+[-./]MM[-./]dd\s*", string.Empty).Trim();
        }
        if (!Use24Hour)
        {
            // HH → hh，必要时追加 tt（AM/PM）
            if (fmt.Contains("HH"))
            {
                fmt = fmt.Replace("HH", "hh");
                if (!fmt.Contains("tt")) fmt = fmt + " tt";
            }
        }
        return string.IsNullOrWhiteSpace(fmt) ? "HH:mm" : fmt;
    }
}
