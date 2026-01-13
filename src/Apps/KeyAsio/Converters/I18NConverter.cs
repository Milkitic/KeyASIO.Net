using System.Globalization;
using Avalonia.Data.Converters;
using KeyAsio.Lang;

namespace KeyAsio.Converters;

public class I18NConverter : IValueConverter
{
    public static readonly I18NConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string key)
        {
            var localized = SR.ResourceManager.GetString(key);
            return string.IsNullOrEmpty(localized) ? key : localized;
        }

        return value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
