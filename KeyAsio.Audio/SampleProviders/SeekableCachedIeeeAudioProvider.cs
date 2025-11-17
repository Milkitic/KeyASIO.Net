using KeyAsio.Audio.Caching;
using NAudio.Wave;

namespace KeyAsio.Audio.SampleProviders;

public class SeekableCachedIeeeAudioProvider : ISampleProvider
{
    private readonly CachedIeeeAudio _cachedAudio;
    private readonly Lock _sourceSoundLock = new();
    private readonly float[] _audioData;
    private readonly int _preSamples;
    private int _position;

    public SeekableCachedIeeeAudioProvider(CachedIeeeAudio cachedAudio, int leadInMilliseconds = 0)
    {
        _cachedAudio = cachedAudio;
        if (leadInMilliseconds != 0)
        {
            _preSamples = TimeSpanToSamples(TimeSpan.FromMilliseconds(leadInMilliseconds));
            _position = _preSamples;
        }

        _audioData = cachedAudio.AudioData;
    }

    public WaveFormat WaveFormat => _cachedAudio.WaveFormat;

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
            var availableSamples = (_audioData.Length + _preSamples) - _position;
            if (availableSamples <= 0) return 0;

            var samplesToCopy = Math.Min(availableSamples, count); //4000

            if (_preSamples == 0)
            {
                _audioData.AsSpan()
                    .Slice(_position, samplesToCopy)
                    .CopyTo(buffer.AsSpan(offset));
                _position += samplesToCopy;
                return samplesToCopy;
            }

            var preLeft = _preSamples - _position; //1200-1000
            if (preLeft <= 0)
            {
                try
                {
                    _audioData.AsSpan()
                        .Slice(_position - _preSamples, samplesToCopy)
                        .CopyTo(buffer.AsSpan(offset));
                }
                catch (Exception ex)
                {
                    throw new Exception("Case: arr", ex);
                }
            }
            else if (preLeft >= samplesToCopy)
            {
                try
                {
                    buffer.AsSpan(offset, samplesToCopy).Fill(0f);
                }
                catch (Exception ex)
                {
                    throw new Exception("Case: num", ex);
                }
            }
            else
            {
                try
                {
                    buffer.AsSpan(offset, preLeft).Fill(0f);
                    _audioData.AsSpan()
                        .Slice(0, samplesToCopy - preLeft)
                        .CopyTo(buffer.AsSpan(offset + preLeft));
                }
                catch (Exception ex)
                {
                    throw new Exception("Case: mix", ex);
                }
            }

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