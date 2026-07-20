using System.Globalization;
using Avalonia.Data.Converters;

namespace KeyAsio.Converters;

public sealed class AsioToAudioConverter : IValueConverter
{
    public static readonly AsioToAudioConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is string text ? text.Replace("ASIO", "Audio", StringComparison.Ordinal) : value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
