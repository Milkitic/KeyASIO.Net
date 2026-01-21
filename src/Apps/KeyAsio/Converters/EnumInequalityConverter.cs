using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace KeyAsio.Converters;

public class EnumInequalityConverter : IValueConverter, IMultiValueConverter
{
    public static readonly EnumInequalityConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || parameter == null) return true; // If null, assume not equal? Or false? 
        // If value is null, it's not equal to parameter (unless parameter is null).
        // Let's stick to simple inequality.
        return !value.Equals(parameter);
    }

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2 || values[0] == null || values[1] == null) return true;
        return !values[0]!.Equals(values[1]);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return BindingOperations.DoNothing;
    }
}