using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace KeyAsio.Converters;

public class EnumEqualityConverter : IValueConverter, IMultiValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || parameter == null) return false;
        return value.Equals(parameter);
    }

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2 || values[0] == null || values[1] == null) return false;
        return values[0]!.Equals(values[1]);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? parameter : BindingOperations.DoNothing;
    }
}