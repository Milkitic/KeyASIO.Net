using NAudio.Wave;

namespace KeyAsio.Core.Audio.SampleProviders.Limiters;

public class DynamicLimiterProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private ISampleProvider? _currentLimiter;
    private readonly Lock _lock = new();

    public DynamicLimiterProvider(ISampleProvider source, LimiterType initialType)
    {
        _source = source;
        UpdateLimiter(initialType);
    }

    public WaveFormat WaveFormat => _source.WaveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        lock (_lock)
        {
            if (_currentLimiter != null)
            {
                return _currentLimiter.Read(buffer, offset, count);
            }
        }

        return _source.Read(buffer, offset, count);
    }

    public void UpdateLimiter(LimiterType type)
    {
        lock (_lock)
        {
            // Create new limiter
            _currentLimiter = CreateLimiterProvider(_source, type);
        }
    }

    private static ISampleProvider? CreateLimiterProvider(ISampleProvider source, LimiterType limiterType)
    {
        return limiterType switch
        {
            LimiterType.Off => null,
            LimiterType.Master => MasterLimiterProvider.UltraLowLatencyPreset(source),
            LimiterType.Quadratic => new QuadraticLimitProvider(source),
            LimiterType.Soft => new SoftLimiterProvider(source),
            LimiterType.Polynomial => new PolynomialLimiterProvider(source),
            _ => MasterLimiterProvider.UltraLowLatencyPreset(source)
        };
    }
}
