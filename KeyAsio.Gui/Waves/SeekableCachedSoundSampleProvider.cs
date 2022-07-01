using System;
using Milki.Extensions.MixPlayer.NAudioExtensions.Wave;
using NAudio.Wave;

namespace KeyAsio.Gui.Waves;

public class SeekableCachedSoundSampleProvider : ISampleProvider
{
    private readonly CachedSound _sourceSound;
    private readonly float[] _audioData;
    private readonly int _preSamples;
    private int _position;

    public SeekableCachedSoundSampleProvider(in CachedSound cachedSound, int leadinMilliseconds = 0)
    {
        _sourceSound = cachedSound;
        if (leadinMilliseconds == 0)
        {
            _audioData = cachedSound.AudioData;
        }
        else
        {
            _preSamples = TimeSpanToSamples(TimeSpan.FromMilliseconds(leadinMilliseconds));
            _position = _preSamples;
            _audioData = new float[cachedSound.AudioData.Length + _preSamples];
            cachedSound.AudioData.CopyTo(_audioData, _preSamples);
        }
    }

    public WaveFormat WaveFormat => _sourceSound.WaveFormat;

    public TimeSpan PlayTime
    {
        get
        {
            lock (_sourceSound)
            {
                return SamplesToTimeSpan(_position - _preSamples);
            }
        }
        set
        {
            lock (_sourceSound)
            {
                _position = TimeSpanToSamples(value) + _preSamples;
            }
        }
    }

    public int Read(float[] buffer, int offset, int count)
    {
        lock (_sourceSound)
        {
            var availableSamples = _audioData.Length - _position;
            if (availableSamples <= 0) return 0;
            var samplesToCopy = Math.Min(availableSamples, count);
            _audioData.AsSpan()
                .Slice(_position, samplesToCopy)
                .CopyTo(buffer.AsSpan(offset));
            _position += samplesToCopy;
            return samplesToCopy;
        }
    }

    private int TimeSpanToSamples(TimeSpan timeSpan)
    {
        var samples = (int)(timeSpan.TotalSeconds * WaveFormat.SampleRate) * WaveFormat.Channels;
        return samples;
    }

    private TimeSpan SamplesToTimeSpan(int samples)
    {
        return WaveFormat.Channels switch
        {
            1 => TimeSpan.FromSeconds((samples) / (double)WaveFormat.SampleRate),
            2 => TimeSpan.FromSeconds((samples >> 1) / (double)WaveFormat.SampleRate),
            4 => TimeSpan.FromSeconds((samples >> 2) / (double)WaveFormat.SampleRate),
            8 => TimeSpan.FromSeconds((samples >> 3) / (double)WaveFormat.SampleRate),
            _ => TimeSpan.FromSeconds((samples / WaveFormat.Channels) / (double)WaveFormat.SampleRate)
        };
    }
}