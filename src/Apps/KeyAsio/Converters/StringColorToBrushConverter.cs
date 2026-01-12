using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Styling;

namespace KeyAsio.Converters;

public class StringColorToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string colorStr)
        {
            if (Color.TryParse(colorStr, out var color))
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

            if (Application.Current?.TryFindResource(colorStr, Application.Current?.ActualThemeVariant, out var res) != true) return null;
            {
                switch (res)
                {
                    case Color c when parameter != null:
                        double opacity = 1.0;
                        if (parameter is string paramStr && double.TryParse(paramStr, NumberStyles.Any,
                                CultureInfo.InvariantCulture, out var parsedOpacity))
                        {
                            opacity = parsedOpacity;
                        }
                        else if (parameter is double paramDouble)
                        {
                            opacity = paramDouble;
                        }

                        return new SolidColorBrush(c, opacity);
                    case Color c:
                        return new SolidColorBrush(c);
                    case IBrush b:
                        return b;
                    default:
                        return null;
                }
            }
        }

        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
