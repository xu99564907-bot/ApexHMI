using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ApexHMI.Converters;

#region 布尔到指示器颜色转换器

/// <summary>
/// 布尔值转指示器背景色
/// </summary>
public class BoolToIndicatorBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b && b)
            return new SolidColorBrush(Color.FromRgb(236, 253, 245)); // #ECFDF5
        return new SolidColorBrush(Color.FromRgb(243, 244, 246)); // #F3F4F6
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>
/// 布尔值转指示器边框色
/// </summary>
public class BoolToIndicatorBorderConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b && b)
            return new SolidColorBrush(Color.FromRgb(16, 185, 129)); // #10B981
        return new SolidColorBrush(Color.FromRgb(229, 231, 235)); // #E5E7EB
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>
/// 布尔值转指示器文字色
/// </summary>
public class BoolToIndicatorTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b && b)
            return new SolidColorBrush(Color.FromRgb(5, 150, 105)); // #059669
        return new SolidColorBrush(Color.FromRgb(156, 163, 175)); // #9CA3AF
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

#endregion

#region 蓝色指示器转换器

public class BoolToBlueBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b && b)
            return new SolidColorBrush(Color.FromRgb(239, 246, 255)); // #EFF6FF
        return new SolidColorBrush(Color.FromRgb(243, 244, 246)); // #F3F4F6
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

public class BoolToBlueBorderConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b && b)
            return new SolidColorBrush(Color.FromRgb(59, 130, 246)); // #3B82F6
        return new SolidColorBrush(Color.FromRgb(229, 231, 235)); // #E5E7EB
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

public class BoolToBlueTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b && b)
            return new SolidColorBrush(Color.FromRgb(37, 99, 235)); // #2563EB
        return new SolidColorBrush(Color.FromRgb(156, 163, 175)); // #9CA3AF
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

#endregion

#region 黄色指示器转换器

public class BoolToYellowBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b && b)
            return new SolidColorBrush(Color.FromRgb(255, 251, 235)); // #FFFBEB
        return new SolidColorBrush(Color.FromRgb(243, 244, 246)); // #F3F4F6
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

public class BoolToYellowBorderConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b && b)
            return new SolidColorBrush(Color.FromRgb(245, 158, 11)); // #F59E0B
        return new SolidColorBrush(Color.FromRgb(229, 231, 235)); // #E5E7EB
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

public class BoolToYellowTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b && b)
            return new SolidColorBrush(Color.FromRgb(217, 119, 6)); // #D97706
        return new SolidColorBrush(Color.FromRgb(156, 163, 175)); // #9CA3AF
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

#endregion

#region 错误指示器转换器

public class BoolToErrorBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b && b)
            return new SolidColorBrush(Color.FromRgb(254, 242, 242)); // #FEF2F2
        return new SolidColorBrush(Color.FromRgb(243, 244, 246)); // #F3F4F6
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

public class BoolToErrorBorderConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b && b)
            return new SolidColorBrush(Color.FromRgb(239, 68, 68)); // #EF4444
        return new SolidColorBrush(Color.FromRgb(229, 231, 235)); // #E5E7EB
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

public class BoolToErrorTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b && b)
            return new SolidColorBrush(Color.FromRgb(220, 38, 38)); // #DC2626
        return new SolidColorBrush(Color.FromRgb(156, 163, 175)); // #9CA3AF
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

#endregion

#region 错误状态专用转换器

public class BoolToErrorBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b && b)
            return new SolidColorBrush(Color.FromRgb(254, 242, 242)); // #FEF2F2
        return new SolidColorBrush(Color.FromRgb(249, 250, 251)); // #F9FAFB
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

public class BoolToErrorValueConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b && b)
            return new SolidColorBrush(Color.FromRgb(220, 38, 38)); // #DC2626
        return new SolidColorBrush(Color.FromRgb(16, 185, 129)); // #10B981
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

public class BoolToStatusTitleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool b && b ? "错误信息" : "状态信息";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

public class BoolToStatusTitleColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b && b)
            return new SolidColorBrush(Color.FromRgb(252, 165, 165)); // #FCA5A5
        return new SolidColorBrush(Color.FromRgb(107, 114, 128)); // #6B7280
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

public class BoolToStatusMessageConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool b && b 
            ? "伺服报警 - 请检查电机连接并复位" 
            : "机械手就绪，等待指令...";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

public class BoolToStatusMessageColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b && b)
            return new SolidColorBrush(Color.FromRgb(254, 226, 226)); // #FEE2E2
        return new SolidColorBrush(Color.FromRgb(55, 65, 81)); // #374151
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

#endregion

#region 错误边框转换器

public class BoolToErrorBorderBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b && b)
            return new SolidColorBrush(Color.FromRgb(185, 28, 28)); // #B91C1C
        return new SolidColorBrush(Color.FromRgb(229, 231, 235)); // #E5E7EB
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

#endregion

#region 错误文字颜色转换器

public class ErrorToTextColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b && b)
            return new SolidColorBrush(Color.FromRgb(248, 113, 113)); // #F87171
        return new SolidColorBrush(Color.FromRgb(17, 24, 39)); // #111827
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

#endregion
