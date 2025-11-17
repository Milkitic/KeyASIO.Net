using KeyAsio.Audio.Caching;
using NAudio.Wave;

namespace KeyAsio.Audio.SampleProviders;

public class CachedIeeeAudioProvider : ISampleProvider
{
    private readonly CachedIeeeAudio _cachedAudio;
    private int _position;

    public CachedIeeeAudioProvider(CachedIeeeAudio cachedAudio)
    {
        _cachedAudio = cachedAudio;
    }

    public int Read(float[] buffer, int offset, int count)
    {
        var availableSamples = _cachedAudio.AudioData.Length - _position;
        var samplesToCopy = Math.Min(availableSamples, count);
        _cachedAudio.AudioData.AsSpan()
            .Slice(_position, samplesToCopy)
            .CopyTo(buffer.AsSpan(offset));
        _position += samplesToCopy;
        return samplesToCopy;
    }

    public WaveFormat WaveFormat => _cachedAudio.WaveFormat;
}