using System.Runtime.CompilerServices;
using KeyAsio.Core.Audio.Caching;
using KeyAsio.Core.Audio.Utils;
using NAudio.Wave;

namespace KeyAsio.Core.Audio.SampleProviders;

public sealed class CachedAudioProvider : IRecyclableProvider, IPoolable
{
    private readonly Lock _sourceSoundLock = new();

    private CachedAudio? _cachedAudio;

    private int _channels;
    private int _sampleRate;
    private int _totalSamples;

    private double _inverseSampleRate; // 预计算 1/SampleRate 用于乘法代替除法
    private int _position;

    public CachedAudioProvider()
    {
    }

    public CachedAudioProvider(CachedAudio cachedAudio)
    {
        Initialize(cachedAudio);
    }

    public void Initialize(CachedAudio cachedAudio)
    {
        _cachedAudio = cachedAudio;

        _sampleRate = cachedAudio.WaveFormat.SampleRate;
        _channels = cachedAudio.WaveFormat.Channels;

        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(
            cachedAudio.WaveFormat.SampleRate,
            cachedAudio.WaveFormat.Channels);

        _inverseSampleRate = 1.0 / _sampleRate;
        // 计算音频数据的总样本数 (Bytes / 2)
        _totalSamples = _cachedAudio.Length / 2;
    }

    public WaveFormat WaveFormat { get; private set; } = null!;
    public bool IsInitialized => _cachedAudio != null;

    public ISampleProvider? ResetAndGetSource()
    {
        Reset();
        return null;
    }

    public TimeSpan PlayTime
    {
        get
        {
            var position = Volatile.Read(ref _position);
            return SamplesToTimeSpan(position);
        }
        set
        {
            lock (_sourceSoundLock)
            {
                _position = TimeSpanToSamples(value);
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
            if (_cachedAudio?.TryAcquirePointer(out byte* pSrcBase) != true || pSrcBase == null)
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

                    int readOffset = _position;
                    if (readOffset >= 0)
                    {
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
        var silenceCount = -_position;

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int TimeSpanToSamples(TimeSpan timeSpan) => (int)(timeSpan.TotalSeconds * _sampleRate) * _channels;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private TimeSpan SamplesToTimeSpan(int samples) => TimeSpan.FromSeconds(samples * _inverseSampleRate / _channels);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _cachedAudio = null;
        WaveFormat = null!;
        _position = 0;
    }

    public bool ExcludeFromPool { get; init; }
}