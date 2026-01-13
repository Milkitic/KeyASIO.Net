using System.ComponentModel;
using System.Globalization;
using Avalonia.Data.Converters;
using KeyAsio.Lang;

namespace KeyAsio.Converters;

public class EnumDescriptionConverter : IValueConverter, IMultiValueConverter
{
    public static readonly EnumDescriptionConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Enum enumValue)
        {
            var field = value.GetType().GetField(value.ToString());
            if (field != null)
            {
                var attributes = field.GetCustomAttributes(typeof(DescriptionAttribute), false);
                if (attributes.Length > 0)
                {
                    var description = ((DescriptionAttribute)attributes[0]).Description;
                    return SR.ResourceManager.GetString(description) ?? description;
                }
            }

            return value.ToString();
        }

        return value;
    }

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values is { Count: > 0 })
        {
            return Convert(values[0], targetType, parameter, culture);
        }

        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}