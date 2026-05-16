#nullable enable
using System;
using System.Globalization;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using ApexHMI.Models.RuntimeUi;
using ApexHMI.Services.RuntimeUi;

namespace ApexHMI.ViewModels.Runtime;

/// <summary>
/// 日期时间域：SystemTime 模式 1s 刷新本地时间；
/// PlcTime / Output 模式订阅 TagId（时间戳字符串）；
/// Input 模式留 TextBox 手输（第一版不接 DatePicker）。
/// </summary>
public partial class DateTimeWidgetViewModel : WidgetViewModelBase
{
    [ObservableProperty] private string _displayText = string.Empty;
    [ObservableProperty] private string _editText = string.Empty;
    [ObservableProperty] private DateTime? _editDate = DateTime.Today;
    [ObservableProperty] private string _editTime = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);

    private DispatcherTimer? _timer;

    public DateTimeWidgetViewModel(WidgetInstance model, IWidgetDataContext dataContext)
        : base(model, dataContext)
    {
        switch (Mode)
        {
            case "SystemTime":
                StartTimer();
                Tick(null, EventArgs.Empty);
                break;
            case "PlcTime":
            case "Output":
            case "Input":
            case "InputOutput":
                var tag = ResolveTag();
                if (!string.IsNullOrWhiteSpace(tag))
                    dataContext.RegisterValueCallback(tag, OnTagValueChanged);
                break;
        }
    }

    public string Mode       => Prop("mode",       "SystemTime");
    public string Format     => Prop("format",     "yyyy-MM-dd HH:mm:ss");
    public string Background => Prop("background", "#FFFFFF");
    public string Foreground => Prop("foreground", "#0F172A");

    public bool IsInput => Mode is "Input" or "InputOutput";

    private string? ResolveTag()
    {
        var v = Prop("variable", "");
        if (!string.IsNullOrWhiteSpace(v)) return v;
        return Model.Binding?.TagId;
    }

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

    protected override void OnTagValueChanged(string rawValue)
    {
        if (DateTime.TryParse(rawValue, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dt))
        {
            try { DisplayText = dt.ToString(Format, CultureInfo.InvariantCulture); }
            catch (FormatException) { DisplayText = dt.ToString(CultureInfo.InvariantCulture); }
            EditDate = dt.Date;
            EditTime = dt.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        }
        else
        {
            DisplayText = rawValue;
        }
    }

    /// <summary>Input 模式：写回组合后的日期时间到 Tag。</summary>
    public void CommitDateTime()
    {
        if (!IsInput) return;
        var tag = ResolveTag();
        if (string.IsNullOrWhiteSpace(tag)) return;
        var date = EditDate ?? DateTime.Today;
        if (!TimeSpan.TryParse(EditTime, CultureInfo.InvariantCulture, out var t))
            t = DateTime.Now.TimeOfDay;
        var dt = date.Date + t;
        var s = dt.ToString(Format, CultureInfo.InvariantCulture);
        _dataContext.ExecuteAction("write-string", $"{tag}|{s}");
    }
}
