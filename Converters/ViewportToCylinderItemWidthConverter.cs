using System;
using System.Globalization;
using System.Windows.Data;

namespace ApexHMI.Converters
{
    public class ViewportToCylinderItemWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is double viewportWidth) || double.IsNaN(viewportWidth) || viewportWidth <= 0)
                return 292.0;

            var mode = parameter?.ToString();

            if (string.Equals(mode, "Axis", StringComparison.OrdinalIgnoreCase))
            {
                const double axisOuterPadding = 24.0;
                const double axisGap = 20.0;          // 两列之间的间距
                var axisAvailable = Math.Max(960.0, viewportWidth - axisOuterPadding);
                var axisWidth = Math.Floor((axisAvailable - axisGap) / 2.0);
                return Math.Max(440.0, axisWidth);
            }

            const double outerPadding = 52.0;
            var available = Math.Max(220.0, viewportWidth - outerPadding);
            const double baseline = 300.0;

            int columns = (int)Math.Floor(available / baseline);
            if (columns < 1) columns = 1;

            double spacingTotal = Math.Max(0, (columns - 1) * 10.0);
            double itemWidth = Math.Floor((available - spacingTotal) / columns);

            if (itemWidth < 220.0) itemWidth = 220.0;
            if (itemWidth > 360.0) itemWidth = 360.0;

            return itemWidth;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
