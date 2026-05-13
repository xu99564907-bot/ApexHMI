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

    public bool IsDigital => string.Equals(ModeProp, "digital", System.StringComparison.OrdinalIgnoreCase);

    private void StartTimer()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += Tick;
        _timer.Start();
    }

    private void Tick(object? sender, EventArgs e)
    {
        try { DisplayText = DateTime.Now.ToString(Format, CultureInfo.InvariantCulture); }
        catch (FormatException) { DisplayText = DateTime.Now.ToString(CultureInfo.InvariantCulture); }
    }
}
