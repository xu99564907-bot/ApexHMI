using System;
using System.Globalization;
using System.Windows.Data;

namespace ApexHMI.Converters;

public class ViewportToContentWidthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not double viewportWidth || double.IsNaN(viewportWidth) || viewportWidth <= 0)
        {
            return 1280.0;
        }

        const double marginReserve = 32.0;
        return Math.Max(320.0, viewportWidth - marginReserve);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
