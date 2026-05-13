using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ApexHMI.Converters;

/// <summary>
/// 将以空格 / 逗号分隔的数字字符串转换为 <see cref="DoubleCollection"/>。
/// 空字符串 → null（解析为实线）。
/// </summary>
public class StringToDoubleCollectionConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var s = value?.ToString();
        if (string.IsNullOrWhiteSpace(s)) return null;
        try
        {
            return DoubleCollection.Parse(s);
        }
        catch
        {
            return null;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value?.ToString() ?? string.Empty;
}
