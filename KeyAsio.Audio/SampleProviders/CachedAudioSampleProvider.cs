using KeyAsio.Audio.Caching;
using NAudio.Wave;

namespace KeyAsio.Audio.SampleProviders;

public class CachedAudioSampleProvider : ISampleProvider
{
    private readonly CachedAudio _cachedAudio;
    private int _position;

    public CachedAudioSampleProvider(CachedAudio cachedAudio)
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