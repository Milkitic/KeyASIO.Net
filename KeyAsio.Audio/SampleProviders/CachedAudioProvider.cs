using KeyAsio.Audio.Caching;
using KeyAsio.Audio.Utils;
using NAudio.Wave;

namespace KeyAsio.Audio.SampleProviders;

[Obsolete]
public sealed class CachedAudioProvider : ISampleProvider
{
    private readonly CachedAudio _cachedAudio;
    private readonly int _totalSamples;
    private int _position; // Sample的位置

    public CachedAudioProvider(CachedAudio cachedAudio)
    {
        _cachedAudio = cachedAudio;
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(
            cachedAudio.WaveFormat.SampleRate,
            cachedAudio.WaveFormat.Channels);
        // 计算总样本数：字节数 / 2 (16-bit = 2 bytes)
        _totalSamples = cachedAudio.Length / 2;
    }

    public WaveFormat WaveFormat { get; }

    public unsafe int Read(float[] buffer, int offset, int count)
    {
        if (count == 0) return 0;

        var readOffset = _position;
        var availableSamples = _totalSamples - readOffset;
        if (availableSamples <= 0) return 0;

        if (!_cachedAudio.TryAcquirePointer(out byte* pSrcBase) || pSrcBase == null)
        {
            return 0;
        }

        try
        {
            var samplesToCopy = Math.Min(availableSamples, count);
            var pSourceShorts = (short*)pSrcBase;
            short* pSrcCurrent = pSourceShorts + readOffset;

            fixed (float* pBuffer = buffer)
            {
                var pTarget = pBuffer + offset;
                SimdAudioConverter.Convert16BitToFloatUnsafe(
                    pSrcCurrent,
                    pTarget,
                    samplesToCopy
                );
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