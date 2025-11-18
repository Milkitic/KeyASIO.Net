using System.Runtime.InteropServices;
using KeyAsio.Audio.Caching;
using KeyAsio.Audio.Utils;
using NAudio.Wave;

namespace KeyAsio.Audio.SampleProviders;

public class CachedAudioProvider : ISampleProvider
{
    private readonly CachedAudio _cachedAudio;
    private int _position; // Sample的位置

    public CachedAudioProvider(CachedAudio cachedAudio)
    {
        _cachedAudio = cachedAudio;
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(
            cachedAudio.WaveFormat.SampleRate,
            cachedAudio.WaveFormat.Channels);
    }

    public WaveFormat WaveFormat { get; }

    public int Read(float[] buffer, int offset, int count)
    {
        var span = _cachedAudio.Span;
        if (span.IsEmpty) return 0;

        // 计算总样本数：字节数 / 2 (16-bit = 2 bytes)
        var totalSamples = span.Length / 2;
        var availableSamples = totalSamples - _position;

        if (availableSamples <= 0) return 0;

        var samplesToCopy = Math.Min(availableSamples, count);

        // 获取源数据的 Span (byte)
        var sourceBytesSpan = span.Slice(_position * 2, samplesToCopy * 2); // x2 因为是字节位置

        // 将 byte 视作 short (PCM-16)
        var sourceShortsSpan = MemoryMarshal.Cast<byte, short>(sourceBytesSpan);

        // 目标 Buffer
        var targetSpan = buffer.AsSpan(offset, samplesToCopy);

        SimdAudioConverter.Convert16BitToFloat(sourceShortsSpan, targetSpan);

        _position += samplesToCopy;
        return samplesToCopy;
    }
}