using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace KeyAsio.Converters;

public class StringColorToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string colorStr && Color.TryParse(colorStr, out var color))
        {
            double opacity = 1.0;
            if (parameter is string paramStr && double.TryParse(paramStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedOpacity))
            {
                opacity = parsedOpacity;
            }
            else if (parameter is double paramDouble)
            {
                opacity = paramDouble;
            }

            return new SolidColorBrush(color, opacity);
        }
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
