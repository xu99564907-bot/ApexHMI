#nullable enable
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Markup;

namespace ApexHMI.Converters;

/// <summary>B2A: bool → FontWeights.Bold / Normal。schema 字段 fontBold。</summary>
public sealed class BoolToFontWeightConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => ParseBool(value) ? FontWeights.Bold : FontWeights.Normal;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is FontWeight fw && fw == FontWeights.Bold;

    private static bool ParseBool(object? raw)
    {
        var s = raw?.ToString();
        return !string.IsNullOrEmpty(s) &&
               (s.Equals("true", StringComparison.OrdinalIgnoreCase) || s == "1" || s.Equals("yes", StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>B2A: bool → FontStyles.Italic / Normal。schema 字段 fontItalic。</summary>
public sealed class BoolToFontStyleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => ParseBool(value) ? FontStyles.Italic : FontStyles.Normal;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is FontStyle fs && fs == FontStyles.Italic;

    private static bool ParseBool(object? raw)
    {
        var s = raw?.ToString();
        return !string.IsNullOrEmpty(s) &&
               (s.Equals("true", StringComparison.OrdinalIgnoreCase) || s == "1");
    }
}

/// <summary>
/// B2A: bool → TextDecorations.Underline / null。schema 字段 fontUnderline。
/// 也可通过 ConverterParameter="Strikethrough" 复用为删除线 converter。
/// </summary>
public sealed class BoolToTextDecorationsConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var enabled = ParseBool(value);
        if (!enabled) return null;
        var mode = parameter?.ToString();
        return mode?.Equals("Strikethrough", StringComparison.OrdinalIgnoreCase) == true
            ? TextDecorations.Strikethrough
            : TextDecorations.Underline;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is TextDecorationCollection tdc && tdc.Count > 0;

    private static bool ParseBool(object? raw)
    {
        var s = raw?.ToString();
        return !string.IsNullOrEmpty(s) &&
               (s.Equals("true", StringComparison.OrdinalIgnoreCase) || s == "1");
    }
}

/// <summary>
/// B2A: 边框样式 字符串（Solid/None/Dashed/Dotted/Double）→ BorderBrush。
/// 第一版仅在 None 时返回 Transparent，其余照常返回原始色（DashArray 不支持 WPF Border，TODO 用 Rectangle 模拟）。
/// </summary>
public sealed class BorderStyleVisibilityConverter : IMultiValueConverter
{
    /// <summary>输入：[color string, style string]；输出：SolidColorBrush（style=None 时返回 Transparent）。</summary>
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values == null || values.Length < 2) return System.Windows.Media.Brushes.Transparent;
        var style = values[1]?.ToString() ?? "Solid";
        if (string.Equals(style, "None", StringComparison.OrdinalIgnoreCase))
            return System.Windows.Media.Brushes.Transparent;
        var color = values[0]?.ToString();
        if (string.IsNullOrWhiteSpace(color)) return System.Windows.Media.Brushes.Transparent;
        try
        {
            return (SolidColorBrush)new BrushConverter().ConvertFromString(color!)!;
        }
        catch { return System.Windows.Media.Brushes.LightGray; }
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
