using NAudio.Wave;

namespace KeyAsio.Core.Audio.SampleProviders.Limiters;

/// <summary>
/// A high-performance zero-latency soft limiter designed for rhythm games.
/// It leaves quiet signals untouched and gently saturates peaks using a cubic polynomial curve.
/// </summary>
public sealed class PolynomialLimiterProvider : LimiterBase
{
    private float _threshold;
    private float _ceiling;
    private float _maxOver;

    public PolynomialLimiterProvider(ISampleProvider source, float threshold = 0.8f, float ceiling = 0.99f) :
        base(source)
    {
        UpdateParameters(threshold, ceiling);
    }

    protected override void Process(float[] buffer, int offset, int count)
    {
        float threshold = _threshold;
        float ceiling = _ceiling;
        float maxOver = _maxOver;

        for (int i = 0; i < count; i++)
        {
            int index = offset + i;
            float x = buffer[index];
            float absX = Math.Abs(x);

            if (absX <= threshold) continue;

            // 目标：将 (Threshold, ∞) 映射到 (Threshold, Ceiling)
            // 曲线特性：在 Threshold 处斜率为 1 (平滑过渡)，无穷大时趋向 Ceiling
            float over = absX - threshold;

            // 核心算法：y = x / (1 + x / k)
            // 这个公式极其高效（一次除法），且听感非常像模拟磁带饱和。
            float soft = over / (1.0f + over / maxOver);

            float result = threshold + soft;

            if (result > ceiling) result = ceiling;

            buffer[index] = Math.Sign(x) * result;
        }
    }

    public void UpdateParameters(float threshold, float ceiling)
    {
        _ceiling = Math.Clamp(ceiling, 0.1f, 1.0f);
        _threshold = Math.Clamp(threshold, 0.1f, _ceiling - 0.01f);

        // 预计算最大过冲量
        _maxOver = _ceiling - _threshold;
    }

    public static PolynomialLimiterProvider GamePreset(ISampleProvider sampleProvider)
    {
        return new PolynomialLimiterProvider(sampleProvider);
    }
}