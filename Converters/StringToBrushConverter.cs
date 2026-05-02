using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Serilog;

namespace ApexHMI.Converters;

public class StringToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var color = value?.ToString();
        if (string.IsNullOrWhiteSpace(color))
        {
            return Brushes.LightGray;
        }

        try
        {
            return (SolidColorBrush)new BrushConverter().ConvertFromString(color)!;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "StringToBrushConverter 无法解析颜色 {Color}", color);
            return Brushes.LightGray;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value?.ToString() ?? "#D1D5DB";
    }
}
