using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ApexHMI.Converters;

/// <summary>
/// 将形如 "x1,y1 x2,y2 ..." 的字符串转换为 <see cref="PointCollection"/>。
/// 解析失败 / 空字符串 → 空 PointCollection。
/// </summary>
public class StringToPointCollectionConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var s = value?.ToString();
        if (string.IsNullOrWhiteSpace(s)) return new PointCollection();
        try
        {
            return PointCollection.Parse(s);
        }
        catch
        {
            return new PointCollection();
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value?.ToString() ?? string.Empty;
}
