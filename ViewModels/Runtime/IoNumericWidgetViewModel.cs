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
/// B2B: 按 WinCC IOField (PDF Table 1-50) 加 15 字段 + 输入越限改"拒绝并恢复" + 限值色 + hiddenInput。
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
            // M3.1: 走 quality 回调，驱动 ####/yellow 显示
            dataContext.RegisterValueCallback(tag, (val, q) =>
            {
                CurrentQuality = q;
                OnTagValueChanged(val);
            });
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

    // ====== B2B WinCC 高优字段 ======
    public bool   AcceptOnExit         => ParseBool(Prop("acceptOnExit",  "true"));
    public bool   AcceptOnFull         => ParseBool(Prop("acceptOnFull",  "false"));
    public bool   ClearOnError         => ParseBool(Prop("clearOnError",  "false"));
    public bool   ClearOnFocus         => ParseBool(Prop("clearOnFocus",  "false"));
    public string AboveUpperLimitColor => Prop("aboveUpperLimitColor", "");
    public string BelowLowerLimitColor => Prop("belowLowerLimitColor", "");
    public string TooltipText          => Prop("tooltipText", "");

    // ====== B2B WinCC 中优字段 ======
    public int    FieldLength          => int.TryParse(Prop("fieldLength", "0"), out var x) ? x : 0;
    public bool   EditOnFocus          => ParseBool(Prop("editOnFocus",   "false"));
    public bool   HiddenInput          => ParseBool(Prop("hiddenInput",   "false"));
    public string FormatPattern        => Prop("formatPattern", "");
    public string FormatType           => Prop("formatType", "Decimal");
    public string UnitColor            => Prop("unitColor", "#64748B");
    public int    UnitMargin           => int.TryParse(Prop("unitMargin", "4"), out var x) ? x : 4;

    /// <summary>
    /// B2B: 当前生效背景色。默认=Background；OnTagValueChanged 中根据 min/max 切到 above/belowLimit 色。
    /// M3.1: Bad 质量 → 灰底（覆盖 limit 色）。
    /// </summary>
    public string EffectiveBackground => IsQualityBad ? "#E5E7EB" : (_effectiveBackground ?? Background);
    private string? _effectiveBackground;

    /// <summary>M3.1: Bad 质量 → 红边框（强制覆盖 BorderColor）。</summary>
    public string EffectiveBorderColor => IsQualityBad ? "#DC2626" : BorderColor;

    // B1A: Input 模式即允许显示+输入；InputOutput 仅作迁移兼容（迁移层会替换掉）。
    public bool IsInput  => Mode is "Input" or "InputOutput";
    public bool IsOutput => true; // 任何模式都要显示当前值

    private static bool ParseBool(string s)
        => !string.IsNullOrEmpty(s) && (s.Equals("true", StringComparison.OrdinalIgnoreCase) || s == "1");

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

        // B2B: hiddenInput=true，显示字符全替换为 *
        var visibleText = HiddenInput ? new string('*', formatted.Length) : formatted;

        // M3.1: Bad 质量显示 ####（WinCC 真实行为，掩盖陈旧/错误读数）；Uncertain 在末尾加 ⚠
        if (IsQualityBad)
        {
            DisplayText = "####";
        }
        else
        {
            var withUnit = string.IsNullOrEmpty(Unit) ? visibleText : $"{visibleText} {Unit}";
            DisplayText = IsQualityUncertain ? withUnit + " ⚠" : withUnit;
        }

        if (string.IsNullOrEmpty(EditText)) EditText = formatted;

        // B2B: 越限切背景色（仅 Decimal 类型才比较）
        UpdateLimitBackgroundColor(rawValue);
    }

    /// <summary>M3.1: 质量变化时刷新显示（即使值未变也要切到 ####）。</summary>
    protected override void OnQualityChanged(TagQuality quality)
    {
        OnTagValueChanged(_lastRawValue);
        OnPropertyChanged(nameof(EffectiveBackground));
        OnPropertyChanged(nameof(EffectiveBorderColor));
    }

    /// <summary>B2B: 按 min/max 切 aboveUpperLimitColor / belowLowerLimitColor。</summary>
    private void UpdateLimitBackgroundColor(string rawValue)
    {
        string? target = null;
        if (double.TryParse(rawValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var num))
        {
            if (double.TryParse(MaxValueRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var max)
                && num > max && !string.IsNullOrWhiteSpace(AboveUpperLimitColor))
            {
                target = AboveUpperLimitColor;
            }
            else if (double.TryParse(MinValueRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var min)
                && num < min && !string.IsNullOrWhiteSpace(BelowLowerLimitColor))
            {
                target = BelowLowerLimitColor;
            }
        }
        if (target != _effectiveBackground)
        {
            _effectiveBackground = target;
            OnPropertyChanged(nameof(EffectiveBackground));
        }
    }

    /// <summary>B1A: 按 DataFormat 字段格式化原始值。B2B: formatPattern 优先于 format。</summary>
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
                if (long.TryParse(rawValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var dt))
                {
                    try
                    {
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
                    // B2B: formatPattern 优先（WinCC 风格 '999.99' → .NET '#.##'）
                    var fmt = !string.IsNullOrWhiteSpace(FormatPattern) ? ConvertWinccPattern(FormatPattern) : Format;
                    try { return num.ToString(fmt, CultureInfo.InvariantCulture); }
                    catch (FormatException) { return num.ToString(CultureInfo.InvariantCulture); }
                }
                return rawValue;
        }
    }

    /// <summary>B2B: WinCC formatPattern (9=必显数字, 0=可选) → .NET 格式串。简化版：9→0, 其它字符原样。</summary>
    private static string ConvertWinccPattern(string pattern)
    {
        // '999.99' 在 WinCC 表示整数 3 位 + 小数 2 位（9 = 数字占位）
        // 简单近似：每个 9 → 0（必显）；其余字符（含 .）原样保留
        // 例 '999.99' → '000.00'。这不是完全等价，但满足生产现场可读性。
        return pattern.Replace('9', '0');
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

        // B2A: DataFormat=DateTime
        if (string.Equals(DataFormat, "DateTime", StringComparison.OrdinalIgnoreCase))
        {
            if (!DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out var stamp) &&
                !DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out stamp))
            {
                Log.Warning("IoNumeric DateTime: 输入无法解析为日期时间 {Text}", text);
                NotifyError($"日期时间格式错误：{text}");
                if (ClearOnError) EditText = string.Empty;
                else EditText = FormatByDataFormat(_lastRawValue);
                return;
            }
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
            NotifyError($"无法解析为数字：{text}");
            if (ClearOnError) EditText = string.Empty;
            else EditText = FormatByDataFormat(_lastRawValue);
            return;
        }

        // B2B: 越限改为"拒绝并恢复旧值 + 系统消息"（WinCC 真实行为，不再 clamp）
        if (double.TryParse(MinValueRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var min) && v < min)
        {
            Log.Warning("IoNumeric: 越下限拒绝 v={V} min={Min}", v, min);
            NotifyError($"输入越下限：{v} < {min}");
            if (ClearOnError) EditText = string.Empty;
            else EditText = FormatByDataFormat(_lastRawValue);
            return;
        }
        if (double.TryParse(MaxValueRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var max) && v > max)
        {
            Log.Warning("IoNumeric: 越上限拒绝 v={V} max={Max}", v, max);
            NotifyError($"输入越上限：{v} > {max}");
            if (ClearOnError) EditText = string.Empty;
            else EditText = FormatByDataFormat(_lastRawValue);
            return;
        }

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

    /// <summary>B2B: 获焦处理 — clearOnFocus 时清空 EditText。</summary>
    public void OnFocus()
    {
        if (ClearOnFocus) EditText = string.Empty;
    }

    /// <summary>B2B: 失焦处理 — acceptOnExit=false 时不自动 Commit，仅回车提交。</summary>
    public void OnLostFocus()
    {
        if (AcceptOnExit) Commit();
        else EditText = FormatByDataFormat(_lastRawValue); // 丢弃未提交的输入
    }

    /// <summary>B2B: 文本变化处理 — acceptOnFull=true 时填满 fieldLength 自动 Commit。</summary>
    public void OnTextChanged()
    {
        if (AcceptOnFull && FieldLength > 0 && (EditText?.Length ?? 0) >= FieldLength)
        {
            Commit();
        }
    }

    private void NotifyError(string message)
    {
        if (_dataContext.Shell is ApexHMI.ViewModels.MainViewModel shell)
            shell.SystemMessage = message;
    }
}
