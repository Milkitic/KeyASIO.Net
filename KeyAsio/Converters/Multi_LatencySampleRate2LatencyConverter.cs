using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;

namespace KeyAsio.Converters;

internal class Multi_LatencySampleRate2LatencyConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values is [int playbackLatency, int sampleRate])
        {
            var latency = 1000d / sampleRate * playbackLatency;
            return latency;
        }

        return double.NaN;
    }
}