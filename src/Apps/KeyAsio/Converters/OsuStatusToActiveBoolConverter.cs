using System;
using System.Globalization;
using Avalonia.Data.Converters;
using KeyAsio.Plugins.Abstractions.OsuMemory;

namespace KeyAsio.Converters;

public class OsuStatusToActiveBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is OsuMemoryStatus status)
        {
            return status != OsuMemoryStatus.NotRunning && status != OsuMemoryStatus.Unknown;
        }
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}