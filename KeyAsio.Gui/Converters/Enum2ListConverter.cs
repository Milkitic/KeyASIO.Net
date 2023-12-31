using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;

namespace KeyAsio.Gui.Converters;

public class Enum2ListConverter : IValueConverter
{
    private readonly Type _enumBaseType = typeof(Enum);

    private static readonly Dictionary<Type, Enum[]> CachedList = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (parameter is not Type t || !t.IsSubclassOf(_enumBaseType))
            return DependencyProperty.UnsetValue;
        if (CachedList.TryGetValue(t, out var enums))
            return enums;

        var array = Enum.GetValues(t).Cast<Enum>().ToArray();
        CachedList.Add(t, array);
        return array;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}