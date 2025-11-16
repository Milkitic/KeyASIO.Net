using NAudio.Wave;

namespace KeyAsio.Audio.SampleProviders.Limiters;

/// <summary>
/// 简易版限制器
/// </summary>
public class SimpleLimiterProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private float _threshold = 0.95f;
    private float _ceiling = 0.99f;

    public SimpleLimiterProvider(ISampleProvider source, float threshold = 0.95f, float ceiling = 0.99f)
    {
        _source = source;
        _threshold = threshold;
        _ceiling = ceiling;
    }

    public WaveFormat WaveFormat => _source.WaveFormat;

    public float Threshold
    {
        get => _threshold;
        set => _threshold = Math.Clamp(value, 0f, 1f);
    }

    public float Ceiling
    {
        get => _ceiling;
        set => _ceiling = Math.Clamp(value, 0f, 1f);
    }

    public int Read(float[] buffer, int offset, int count)
    {
        int samplesRead = _source.Read(buffer, offset, count);
        
        for (int i = 0; i < samplesRead; i++)
        {
            int index = offset + i;
            float sample = buffer[index];
            float abs = Math.Abs(sample);
            
            if (abs > _threshold)
            {
                // Soft knee 软拐点压缩
                float excess = abs - _threshold;
                float compressed = _threshold + excess * 0.5f;
                
                // 确保不超过 ceiling
                compressed = Math.Min(compressed, _ceiling);
                
                // 保持符号
                buffer[index] = Math.Sign(sample) * compressed;
            }
        }
        
        return samplesRead;
    }
}