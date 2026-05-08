using System;
using System.Globalization;
using System.Windows.Data;

namespace ApexHMI.Converters;

/// <summary>
/// 字符串相等比较 → 字符串 "True"/"False"。
/// 之所以返回 string 而不是 bool：用作 Button.Tag 时，Style 里
/// `<Trigger Property="Tag" Value="True">` 的 Value 是字符串，bool 类型不匹配会永远失败。
///
/// 单 Binding：value == ConverterParameter
/// MultiBinding：values[0] == values[1]
/// </summary>
public sealed class EqualsToBoolConverter : IValueConverter, IMultiValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => string.Equals(value?.ToString() ?? string.Empty,
                         parameter?.ToString() ?? string.Empty,
                         StringComparison.Ordinal) ? "True" : "False";

    public object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values is null || values.Length < 2) return "False";
        return string.Equals(values[0]?.ToString() ?? string.Empty,
                             values[1]?.ToString() ?? string.Empty,
                             StringComparison.Ordinal) ? "True" : "False";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
