using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using KeyAsio.Audio.Caching;
using KeyAsio.Audio.Utils;
using NAudio.Wave;

namespace KeyAsio.Audio.SampleProviders;

public class SeekableCachedAudioProvider : ISampleProvider
{
    private readonly CachedAudio _cachedAudio;
    private readonly Lock _sourceSoundLock = new();
    private readonly byte[] _audioData;
    private readonly int _preSamples;

    private int _position;

    public SeekableCachedAudioProvider(CachedAudio cachedAudio, int leadInMilliseconds = 0)
    {
        _cachedAudio = cachedAudio;
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(
            cachedAudio.WaveFormat.SampleRate,
            cachedAudio.WaveFormat.Channels);

        if (leadInMilliseconds != 0)
        {
            _preSamples = TimeSpanToSamples(TimeSpan.FromMilliseconds(leadInMilliseconds));
            _position = _preSamples;
        }

        _audioData = cachedAudio.AudioData;
    }

    public WaveFormat WaveFormat { get; }

    public TimeSpan PlayTime
    {
        get
        {
            lock (_sourceSoundLock)
            {
                return SamplesToTimeSpan(_position - _preSamples);
            }
        }
        set
        {
            lock (_sourceSoundLock)
            {
                _position = TimeSpanToSamples(value) + _preSamples;
            }
        }
    }

    public int Read(float[] buffer, int offset, int count)
    {
        lock (_sourceSoundLock)
        {
            // 计算音频数据的总样本数 (Bytes / 2)
            int totalAudioSamples = _audioData.Length / 2;
            var availableSamples = (totalAudioSamples + _preSamples) - _position;

            if (availableSamples <= 0) return 0;

            var samplesToCopy = Math.Min(availableSamples, count);

            // Case 1: 无前置静音，直接读取
            if (_preSamples == 0)
            {
                ReadAndConvert(_position, samplesToCopy, buffer, offset);
                _position += samplesToCopy;
                return samplesToCopy;
            }

            // Case 2: 有前置静音逻辑
            var silenceCount = _preSamples - _position;
            if (silenceCount <= 0)
            {
                // 已经过了静音期，纯音频
                ReadAndConvert(-silenceCount, samplesToCopy, buffer, offset);
            }
            else if (silenceCount >= samplesToCopy)
            {
                // 完全在静音期内
                buffer.AsSpan(offset, samplesToCopy).Clear();
            }
            else
            {
                var audioCount = samplesToCopy - silenceCount;
                // 填静音
                buffer.AsSpan(offset, silenceCount).Clear();
                // 填音频 (从 0 开始)
                ReadAndConvert(0, audioCount, buffer, offset + silenceCount);
            }

            _position += samplesToCopy;
            return samplesToCopy;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ReadAndConvert(int sourceSampleOffset, int sampleCount, float[] targetBuffer, int targetOffset)
    {
        var sourceBytes = _audioData.AsSpan(sourceSampleOffset * 2, sampleCount * 2);
        var sourceShorts = MemoryMarshal.Cast<byte, short>(sourceBytes);
        var targetSpan = targetBuffer.AsSpan(targetOffset, sampleCount);
        SimdAudioConverter.Convert16BitToFloat(sourceShorts, targetSpan);
    }

    private int TimeSpanToSamples(TimeSpan timeSpan)
    {
        return (int)(timeSpan.TotalSeconds * WaveFormat.SampleRate) * WaveFormat.Channels;
    }

    private TimeSpan SamplesToTimeSpan(int samples)
    {
        return WaveFormat.Channels switch
        {
            1 => TimeSpan.FromSeconds((samples) / (double)WaveFormat.SampleRate),
            2 => TimeSpan.FromSeconds((samples >> 1) / (double)WaveFormat.SampleRate),
            4 => TimeSpan.FromSeconds((samples >> 2) / (double)WaveFormat.SampleRate),
            8 => TimeSpan.FromSeconds((samples >> 3) / (double)WaveFormat.SampleRate),
            _ => TimeSpan.FromSeconds(samples / (double)WaveFormat.Channels / WaveFormat.SampleRate)
        };
    }
}