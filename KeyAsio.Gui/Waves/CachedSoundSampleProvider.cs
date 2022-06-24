using System;
using Milki.Extensions.MixPlayer.NAudioExtensions.Wave;
using NAudio.Wave;

namespace KeyAsio.Gui.Waves;

public class CachedSoundSampleProvider : ISampleProvider
{
    private readonly CachedSound _sourceSound;
    private int _position;

    public CachedSoundSampleProvider(in CachedSound cachedSound)
    {
        _sourceSound = cachedSound;
    }

    public int Read(float[] buffer, int offset, int count)
    {
        var availableSamples = _sourceSound.AudioData.Length - _position;
        var samplesToCopy = Math.Min(availableSamples, count);
        _sourceSound.AudioData.AsSpan()
            .Slice(_position, samplesToCopy)
            .CopyTo(buffer.AsSpan(offset));
        _position += samplesToCopy;
        return samplesToCopy;
    }

    public WaveFormat WaveFormat => _sourceSound.WaveFormat;
}