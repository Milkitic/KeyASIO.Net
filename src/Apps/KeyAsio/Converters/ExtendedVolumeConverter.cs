using System.Globalization;
using Avalonia.Data.Converters;

namespace KeyAsio.Converters;

public class ExtendedVolumeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is true)
        {
            return 150.0;
        }

        return 100.0;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}