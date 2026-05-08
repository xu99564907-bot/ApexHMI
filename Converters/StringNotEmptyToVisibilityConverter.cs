using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ApexHMI.Converters;

/// <summary>非空字符串 → Visible，空/null → Collapsed。</summary>
public sealed class StringNotEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
