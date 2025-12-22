using System.Globalization;
using Avalonia.Data.Converters;

namespace KeyAsio.Converters;

public class IntMillisecond2TimeSpanConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int i)
        {
            var sign = i < 0 ? "-" : "";
            var ts = TimeSpan.FromMilliseconds(Math.Abs(i));
            return $"{sign}{ts:mm\\:ss\\.fff}";
        }
        return value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
