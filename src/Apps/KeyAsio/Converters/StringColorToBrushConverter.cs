using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Converters;
using Avalonia.Media;

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

            object? res = null;
            if (Application.Current?.TryFindResource(colorStr, out res) == true ||
                (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
                 desktop.MainWindow?.TryFindResource(colorStr, out res) == true))
            {
                if (res is Color c)
                {
                    // Apply opacity if parameter is present
                    if (parameter != null)
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
                        return new SolidColorBrush(c, opacity);
                    }
                    return new SolidColorBrush(c);
                }

                if (res is IBrush b) return b;
                return null;
            }
        }

        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
