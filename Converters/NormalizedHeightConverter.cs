using System;
using System.Globalization;
using System.Windows.Data;

namespace ApexHMI.Converters;

/// <summary>把 [0, 1] 的归一化值乘以 ConverterParameter 表示的最大高度（默认 100）。</summary>
public sealed class NormalizedHeightConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not double normalized) return 0d;
        var max = 100d;
        if (parameter is string s && double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var p))
        {
            max = p;
        }
        return Math.Max(0, Math.Min(1, normalized)) * max;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
