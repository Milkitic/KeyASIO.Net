using System.Runtime.CompilerServices;
using KeyAsio.Audio.Caching;
using KeyAsio.Audio.Utils;
using NAudio.Wave;

namespace KeyAsio.Audio.SampleProviders;

public sealed class SeekableCachedAudioProvider : ISampleProvider
{
    private readonly CachedAudio _cachedAudio;
    private readonly Lock _sourceSoundLock = new();

    private readonly int _channels;
    private readonly int _sampleRate;

    private readonly int _preSamples;
    private readonly int _totalAudioSamples;
    private readonly int _totalSamples;

    private readonly double _inverseSampleRate; // 预计算 1/SampleRate 用于乘法代替除法

    private int _position;

    public SeekableCachedAudioProvider(CachedAudio cachedAudio, int leadInMilliseconds = 0)
    {
        _cachedAudio = cachedAudio;

        _sampleRate = cachedAudio.WaveFormat.SampleRate;
        _channels = cachedAudio.WaveFormat.Channels;

        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(
            cachedAudio.WaveFormat.SampleRate,
            cachedAudio.WaveFormat.Channels);

        _inverseSampleRate = 1.0 / _sampleRate;
        // 计算音频数据的总样本数 (Bytes / 2)
        _totalAudioSamples = _cachedAudio.Length / 2;

        if (leadInMilliseconds != 0)
        {
            _preSamples = TimeSpanToSamples(TimeSpan.FromMilliseconds(leadInMilliseconds));
            _position = _preSamples;
        }

        _totalSamples = _totalAudioSamples + _preSamples;
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

    public unsafe int Read(float[] buffer, int offset, int count)
    {
        if (count == 0) return 0;

        lock (_sourceSoundLock)
        {
            var availableSamples = _totalSamples - _position;
            if (availableSamples <= 0) return 0;
            if (!_cachedAudio.TryAcquirePointer(out byte* pSrcBase) || pSrcBase == null)
            {
                return 0;
            }

            try
            {
                var samplesToCopy = Math.Min(availableSamples, count);
                fixed (float* pBuffer = buffer)
                {
                    float* pTarget = pBuffer + offset;
                    short* pSourceShorts = (short*)pSrcBase;

                    if (_position >= _preSamples)
                    {
                        int readOffset = _position - _preSamples;
                        var pSrcCurrent = pSourceShorts + readOffset;
                        SimdAudioConverter.Convert16BitToFloatUnsafe(
                            pSrcCurrent,
                            pTarget,
                            samplesToCopy
                        );
                    }
                    else
                    {
                        ProcessWithSilence(pSourceShorts, pTarget, samplesToCopy);
                    }
                }

                _position += samplesToCopy;
                return samplesToCopy;
            }
            finally
            {
                _cachedAudio.ReleasePointer();
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe void ProcessWithSilence(short* pSource, float* pTarget, int samplesToCopy)
    {
        var silenceCount = _preSamples - _position;

        // 如果剩余部分全是静音
        if (silenceCount >= samplesToCopy)
        {
            new Span<float>(pTarget, samplesToCopy).Clear();
        }
        else
        {
            new Span<float>(pTarget, silenceCount).Clear();

            var audioCount = samplesToCopy - silenceCount;
            SimdAudioConverter.Convert16BitToFloatUnsafe(
                pSource,
                pTarget + silenceCount,
                audioCount
            );
        }
    }

    private int TimeSpanToSamples(TimeSpan timeSpan)
    {
        return (int)(timeSpan.TotalSeconds * _sampleRate) * _channels;
    }

    private TimeSpan SamplesToTimeSpan(int samples)
    {
        return TimeSpan.FromSeconds(samples * _inverseSampleRate / _channels);
    }
}