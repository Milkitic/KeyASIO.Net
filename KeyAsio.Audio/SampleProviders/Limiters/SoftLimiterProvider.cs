using NAudio.Wave;

namespace KeyAsio.Audio.SampleProviders.Limiters;

/// <summary>
/// 基于 tanh 的软限制器
/// </summary>
public class SoftLimiterProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private float _drive = 0.9f;  // 驱动强度

    public SoftLimiterProvider(ISampleProvider source, float drive = 0.9f)
    {
        _source = source;
        _drive = drive;
    }

    public WaveFormat WaveFormat => _source.WaveFormat;

    public float Drive
    {
        get => _drive;
        set => _drive = Math.Clamp(value, 0.5f, 1.0f);
    }

    public int Read(float[] buffer, int offset, int count)
    {
        int samplesRead = _source.Read(buffer, offset, count);
        
        for (int i = 0; i < samplesRead; i++)
        {
            int index = offset + i;
            // tanh 软饱和 - 模拟模拟设备的自然限制
            buffer[index] = MathF.Tanh(buffer[index] * _drive) / MathF.Tanh(_drive);
        }
        
        return samplesRead;
    }
}