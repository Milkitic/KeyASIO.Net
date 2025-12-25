using System.ComponentModel;
using System.Globalization;
using Avalonia.Data.Converters;

namespace KeyAsio.Converters;

public class EnumDescriptionConverter : IValueConverter
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
                    return ((DescriptionAttribute)attributes[0]).Description;
                }
            }
            return value.ToString();
        }
        return value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}