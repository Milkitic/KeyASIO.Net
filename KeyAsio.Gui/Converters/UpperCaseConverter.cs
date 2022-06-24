using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;

namespace KeyAsio.Gui.Converters;

internal class UpperCaseConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        return value?.ToString()?.ToUpper();
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class Enum2ListConverter : IValueConverter
{
    private readonly Type _enumBaseType = typeof(Enum);

    private static readonly Dictionary<Type, Enum[]> CachedList = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (parameter is not Type t || !t.IsSubclassOf(_enumBaseType))
            return DependencyProperty.UnsetValue;
        if (CachedList.ContainsKey(t))
            return CachedList[t];

        var array = Enum.GetValues(t).Cast<Enum>().ToArray();
        CachedList.Add(t, array);
        return array;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}