using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ApexHMI.Converters;

/// <summary>
/// 将 null 值转换为 Visibility。
/// 默认：非 null → Visible，null → Collapsed。
/// ConverterParameter="Invert" 时反转。
/// </summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var invert = parameter is string p && p.Equals("Invert", StringComparison.OrdinalIgnoreCase);
        var isNull = value is null;

        if (invert)
            return isNull ? Visibility.Visible : Visibility.Collapsed;

        return isNull ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
