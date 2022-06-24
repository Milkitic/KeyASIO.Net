using System;
using NAudio.Wave;

namespace KeyAsio.Gui.Waves;

public class BalanceSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _sourceProvider;
    private float _leftVolume = 0.5f;
    private float _rightVolume = 0.5f;
    private readonly int _channels;

    public BalanceSampleProvider(ISampleProvider sourceProvider)
    {
        _sourceProvider = sourceProvider;
        _channels = _sourceProvider.WaveFormat.Channels;
        if (_channels > 2)
            throw new NotSupportedException("channels: " + _channels);
        Balance = 0f;
    }


    public float Balance
    {
        get => (_rightVolume - _leftVolume) * 2;
        set
        {
            FixBalanceRange(ref value);

            if (value > 0)
            {
                _leftVolume = 1f - value;
                _rightVolume = 1f + value;
            }
            else if (value < 0)
            {
                _leftVolume = 1f - value;
                _rightVolume = 1f + value;
            }
            else
            {
                _leftVolume = 1f;
                _rightVolume = 1f;
            }
        }
    }

    public WaveFormat WaveFormat => _sourceProvider.WaveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        int samplesRead = _sourceProvider.Read(buffer, offset, count);
        if (_channels != 2) return samplesRead;
        if (Balance == 0) return samplesRead;

        if ((count & 3) == 0)
        {
            for (int n = 0; n < count; n += 4)
            {
                buffer[offset + n] *= _leftVolume;
                buffer[offset + n + 1] *= _rightVolume;

                buffer[offset + n + 2] *= _leftVolume;
                buffer[offset + n + 3] *= _rightVolume;
            }
        }
        else
        {
            for (int n = 0; n < count; n += 2)
            {
                //var oldLeft = buffer[offset + n];
                //var oldRight = buffer[offset + n + 1];
                buffer[offset + n] *= _leftVolume;
                buffer[offset + n + 1] *= _rightVolume;
            }
        }

        return samplesRead;
    }

    private static void FixBalanceRange(ref float value)
    {
        if (value > 1f)
        {
            value = 1f;
        }
        else if (value < -1f)
        {
            value = -1f;
        }
    }
}