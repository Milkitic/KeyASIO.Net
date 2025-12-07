using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;

namespace KeyAsio.Converters;

public class Multi_LatencySampleRate2LatencyConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count == 2 && 
            values[0] is int playbackLatency && 
            values[1] is int sampleRate && sampleRate > 0)
        {
            var latency = 1000d / sampleRate * playbackLatency;
            return latency;
        }

        return 0d;
    }
}
