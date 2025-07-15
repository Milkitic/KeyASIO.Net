using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace KeyAsio.Converters;

/// <summary>
/// ö��ֵ����Ƚ�ת�����������ж�ö��ֵ�Ƿ񲻵���ָ����Ŀ��ֵ
/// </summary>
internal class EnumNotEqualityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Enum selected && parameter is Enum target)
        {
            return !selected.Equals(target);
        }
        
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}