using NAudio.Dsp;
using NAudio.Wave;

namespace KeyAsio.Audio.SampleProviders;

public class LowPassSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _sourceProvider;
    private readonly int _sampleRate;
    private readonly int _channels;
    private readonly BiQuadFilter[] _filters;

    public LowPassSampleProvider(ISampleProvider sourceProvider, int sampleRate, int frequency)
    {
        _sourceProvider = sourceProvider;
        _sampleRate = sampleRate;
        Frequency = frequency;
        _channels = sourceProvider.WaveFormat.Channels;
        _filters = new BiQuadFilter[_channels];

        InitFilters();
    }

    public int Frequency { get; private set; }

    public WaveFormat WaveFormat => _sourceProvider.WaveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        try
        {
            int samplesRead = _sourceProvider.Read(buffer, offset, count);

            for (int i = 0; i < samplesRead; i++)
            {
                buffer[offset + i] = _filters[(i % _channels)].Transform(buffer[offset + i]);
            }

            return samplesRead;
        }
        catch
        {
            buffer.AsSpan(offset, count).Fill(0);
            return count;
        }
    }

    public void SetFrequency(int frequency)
    {
        Frequency = frequency;
        InitFilters();
    }

    private void InitFilters()
    {
        for (int n = 0; n < _channels; n++)
        {
            if (_filters[n] == null!)
            {
                _filters[n] = BiQuadFilter.LowPassFilter(_sampleRate, Frequency, 1);
            }
            else
            {
                _filters[n].SetLowPassFilter(_sampleRate, Frequency, 1);
            }
        }
    }
}