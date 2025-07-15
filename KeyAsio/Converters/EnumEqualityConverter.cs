using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace KeyAsio.Converters;

/// <summary>
/// 枚举值比较转换器，用于判断枚举值是否等于指定的目标值
/// </summary>
internal class EnumEqualityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Enum selected && parameter is Enum target)
        {
            return selected.Equals(target);
        }
        
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}