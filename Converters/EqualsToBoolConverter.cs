using System;
using System.Globalization;
using System.Windows.Data;

namespace ApexHMI.Converters;

/// <summary>
/// 字符串相等比较 → bool。给 DataTrigger / Tag 用。
/// 单 Binding：value == ConverterParameter
/// MultiBinding：values[0] == values[1]
/// </summary>
public sealed class EqualsToBoolConverter : IValueConverter, IMultiValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => string.Equals(value?.ToString() ?? string.Empty,
                         parameter?.ToString() ?? string.Empty,
                         StringComparison.Ordinal);

    public object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values is null || values.Length < 2) return false;
        return string.Equals(values[0]?.ToString() ?? string.Empty,
                             values[1]?.ToString() ?? string.Empty,
                             StringComparison.Ordinal);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
