using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows.Data;
using ApexHMI.Converters;
using Xunit;

namespace ApexHMI.Tests.Converters;

public class RobotConvertersTests
{
    [Fact]
    public void RobotConvertersConvertBackReturnsBindingDoNothing()
    {
        var converterTypes = new[]
        {
            typeof(BoolToIndicatorBrushConverter),
            typeof(BoolToIndicatorBorderConverter),
            typeof(BoolToIndicatorTextConverter),
            typeof(BoolToBlueBrushConverter),
            typeof(BoolToBlueBorderConverter),
            typeof(BoolToBlueTextConverter),
            typeof(BoolToYellowBrushConverter),
            typeof(BoolToYellowBorderConverter),
            typeof(BoolToYellowTextConverter),
            typeof(BoolToErrorBrushConverter),
            typeof(BoolToErrorBorderConverter),
            typeof(BoolToErrorTextConverter),
            typeof(BoolToErrorBackgroundConverter),
            typeof(BoolToErrorValueConverter),
            typeof(BoolToStatusTitleConverter),
            typeof(BoolToStatusTitleColorConverter),
            typeof(BoolToStatusMessageConverter),
            typeof(BoolToStatusMessageColorConverter),
            typeof(BoolToErrorBorderBrushConverter),
            typeof(ErrorToTextColorConverter),
            typeof(ViewportToCylinderItemWidthConverter),
        };

        Assert.NotEmpty(converterTypes);

        foreach (var converterType in converterTypes)
        {
            var converter = (IValueConverter)Activator.CreateInstance(converterType)!;

            var result = converter.ConvertBack(
                value: "ignored",
                targetType: typeof(object),
                parameter: null,
                culture: CultureInfo.InvariantCulture);

            Assert.Same(Binding.DoNothing, result);
        }
    }
}
