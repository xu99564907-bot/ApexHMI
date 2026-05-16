using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ApexHMI.Converters;

/// <summary>
/// Enum / 字符串 ↔ Bool 双向转换器，专门给 RadioButton.IsChecked 用。
/// <para>Convert: value.ToString() == parameter → true / false</para>
/// <para>ConvertBack: true → 把 parameter 解析为 enum value 写回；false → <see cref="Binding.DoNothing"/>。</para>
/// </summary>
public sealed class EnumEqualsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null || parameter is null) return false;
        return string.Equals(value.ToString(), parameter.ToString(), StringComparison.Ordinal);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b && b && parameter is not null)
        {
            if (targetType.IsEnum)
                return Enum.Parse(targetType, parameter.ToString()!, ignoreCase: true);
            // 处理 Nullable<Enum>
            var underlying = Nullable.GetUnderlyingType(targetType);
            if (underlying is { IsEnum: true })
                return Enum.Parse(underlying, parameter.ToString()!, ignoreCase: true);
            return parameter.ToString()!;
        }
        return Binding.DoNothing;
    }
}
