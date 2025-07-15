using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace KeyAsio.Converters;

internal class UpperCaseConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value?.ToString()?.ToUpper();
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}