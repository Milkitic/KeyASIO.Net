using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;

namespace KeyAsio.Converters;

/// <summary>
/// 多值延迟转换器，将延迟帧数和采样率转换为毫秒延迟
/// </summary>
internal class MultiLatencyMillisecondsConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values is [int playbackLatency, int sampleRate])
        {
            // 计算公式：毫秒延迟 = (帧数 / 采样率) * 1000
            var latency = 1000d / sampleRate * playbackLatency;
            return latency;
        }

        return double.NaN;
    }
}