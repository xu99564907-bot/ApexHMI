#nullable enable
using System;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ApexHMI.Models.RuntimeUi;
using ApexHMI.Services.RuntimeUi;
using Serilog;

namespace ApexHMI.ViewModels.Runtime;

/// <summary>
/// 数字 I/O 域：Output 模式订阅 TagId 显示；Input 模式可输入并写回 PLC。
/// 读地址优先级：Properties["variable"] > Model.Binding.TagId。
/// </summary>
public partial class IoNumericWidgetViewModel : WidgetViewModelBase
{
    [ObservableProperty] private string _displayText = string.Empty;
    [ObservableProperty] private string _editText = string.Empty;

    private string _lastRawValue = string.Empty;

    public IoNumericWidgetViewModel(WidgetInstance model, IWidgetDataContext dataContext)
        : base(model, dataContext)
    {
        var tag = ResolveTag();
        if (!string.IsNullOrWhiteSpace(tag))
        {
            dataContext.RegisterValueCallback(tag, OnTagValueChanged);
        }
    }

    /// <summary>Input / Output（B1A: 不再支持 InputOutput，Input 本身允许显示+输入）。</summary>
    public string Mode            => Prop("mode",        "Output");
    /// <summary>B1A: 显示格式 — Decimal / Binary / Hexadecimal / String / DateTime（PDF Page 642）。</summary>
    public string DataFormat      => Prop("dataFormat",  "Decimal");
    public string Format          => Prop("format",      "0.##");
    public string DecimalsRaw     => Prop("decimals",    "2");
    public string Unit            => Prop("unit",        "");
    public string MinValueRaw     => Prop("minValue",    "");
    public string MaxValueRaw     => Prop("maxValue",    "");
    public string TextAlignment   => Prop("textAlign",   "Right");
    public string Background      => Prop("background",  "#FFFFFF");
    public string Foreground      => Prop("foreground",  "#0F172A");
    /// <summary>B2A: 字号（io-numeric schema 自有字段 fontSize）。</summary>
    public string FontSizeRaw     => Prop("fontSize",    "14");

    /// <summary>
    /// B2B 起：当前生效背景色。默认=Background；越上限→aboveUpperLimitColor；越下限→belowLowerLimitColor。
    /// 由 OnTagValueChanged 时计算并 OnPropertyChanged。
    /// </summary>
    public string EffectiveBackground => _effectiveBackground ?? Background;
    private string? _effectiveBackground;

    // B1A: Input 模式即允许显示+输入；InputOutput 仅作迁移兼容（迁移层会替换掉）。
    public bool IsInput  => Mode is "Input" or "InputOutput";
    public bool IsOutput => true; // 任何模式都要显示当前值

    private string? ResolveTag()
    {
        var v = Prop("variable", "");
        if (!string.IsNullOrWhiteSpace(v)) return v;
        return Model.Binding?.TagId;
    }

    protected override void OnTagValueChanged(string rawValue)
    {
        _lastRawValue = rawValue;
        var formatted = FormatByDataFormat(rawValue);
        DisplayText = string.IsNullOrEmpty(Unit) ? formatted : $"{formatted} {Unit}";
        if (string.IsNullOrEmpty(EditText)) EditText = formatted;
    }

    /// <summary>B1A: 按 DataFormat 字段格式化原始值。</summary>
    private string FormatByDataFormat(string rawValue)
    {
        switch (DataFormat)
        {
            case "Binary":
                if (long.TryParse(rawValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var bi))
                    return Convert.ToString(bi, 2);
                return rawValue;
            case "Hexadecimal":
                if (long.TryParse(rawValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var hx))
                    return hx.ToString("X", CultureInfo.InvariantCulture);
                return rawValue;
            case "String":
                return rawValue;
            case "DateTime":
                // 支持两种来源：Unix 秒时间戳，或 .NET ticks
                if (long.TryParse(rawValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var dt))
                {
                    try
                    {
                        // ticks 量级 ~ 10^17 才合理；否则按 Unix 秒处理
                        DateTime stamp = dt > 100000000000L
                            ? new DateTime(dt, DateTimeKind.Utc).ToLocalTime()
                            : DateTimeOffset.FromUnixTimeSeconds(dt).LocalDateTime;
                        var fmt = string.IsNullOrWhiteSpace(Format) || Format == "0.##" ? "yyyy-MM-dd HH:mm:ss" : Format;
                        return stamp.ToString(fmt, CultureInfo.InvariantCulture);
                    }
                    catch { return rawValue; }
                }
                return rawValue;
            case "Decimal":
            default:
                if (double.TryParse(rawValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var num))
                {
                    try { return num.ToString(Format, CultureInfo.InvariantCulture); }
                    catch (FormatException) { return num.ToString(CultureInfo.InvariantCulture); }
                }
                return rawValue;
        }
    }

    [RelayCommand]
    private void Commit()
    {
        if (!IsInput) return;
        var tag = ResolveTag();
        if (string.IsNullOrWhiteSpace(tag)) return;

        // B1C: 写入前权限检查（Properties["authorization"] + RequiredRole）
        if (!CheckAuthorizationAndNotify()) return;

        var text = EditText?.Trim() ?? string.Empty;
        double v;

        // B2A: DataFormat=DateTime 时，先尝试 DateTime.Parse(text) 再转 ticks/Unix 秒
        if (string.Equals(DataFormat, "DateTime", StringComparison.OrdinalIgnoreCase))
        {
            if (!DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out var stamp) &&
                !DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out stamp))
            {
                Log.Warning("IoNumeric DateTime: 输入无法解析为日期时间 {Text}", text);
                if (_dataContext.Shell is ApexHMI.ViewModels.MainViewModel shell)
                    shell.SystemMessage = $"日期时间格式错误：{text}";
                // 恢复显示
                EditText = FormatByDataFormat(_lastRawValue);
                return;
            }
            // 按当前值量级判断目标类型：>1e11 → ticks，否则 Unix 秒
            long target;
            if (long.TryParse(_lastRawValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var existing)
                && existing > 100000000000L)
                target = stamp.ToUniversalTime().Ticks;
            else
                target = new DateTimeOffset(stamp).ToUnixTimeSeconds();
            _dataContext.ExecuteAction("write-int", $"{tag}|{target}");
            return;
        }

        if (!double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out v))
        {
            Log.Warning("IoNumeric: 输入无法解析为数字 {Text}", text);
            if (_dataContext.Shell is ApexHMI.ViewModels.MainViewModel shellNan)
                shellNan.SystemMessage = $"无法解析为数字：{text}";
            EditText = FormatByDataFormat(_lastRawValue);
            return;
        }

        // 限值
        if (double.TryParse(MinValueRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var min) && v < min) v = min;
        if (double.TryParse(MaxValueRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var max) && v > max) v = max;

        // 写回类型按 Format / decimals 推断：小数位>0 用 float，否则 int
        var isInt = int.TryParse(DecimalsRaw, out var d) && d == 0;
        if (isInt)
        {
            _dataContext.ExecuteAction("write-int", $"{tag}|{(long)Math.Round(v)}");
        }
        else
        {
            _dataContext.ExecuteAction("write-float", $"{tag}|{v.ToString(CultureInfo.InvariantCulture)}");
        }
    }
}
