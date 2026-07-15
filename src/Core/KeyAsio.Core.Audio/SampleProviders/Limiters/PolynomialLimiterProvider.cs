using NAudio.Wave;

namespace KeyAsio.Core.Audio.SampleProviders.Limiters;

/// <summary>
/// A high-performance zero-latency soft limiter designed for rhythm games.
/// It leaves quiet signals untouched and gently saturates peaks using a rational curve.
/// </summary>
public sealed class RationalSoftClipper : LimiterBase
{
    private const float MinimumThreshold = 0.1f;
    private const float MinimumSoftRange = 0.01f;
    private const float MaximumCeiling = 1.0f;

    private Parameters _parameters = null!;

    public RationalSoftClipper(ISampleProvider source, float threshold = 0.85f, float ceiling = 0.99f) :
        base(source)
    {
        UpdateParameters(threshold, ceiling);
    }

    protected override void Process(float[] buffer, int offset, int count)
    {
        Parameters parameters = Volatile.Read(ref _parameters);
        float threshold = parameters.Threshold;
        float ceiling = parameters.Ceiling;
        float softRange = parameters.SoftRange;

        for (int i = 0; i < count; i++)
        {
            int index = offset + i;
            float x = buffer[index];

            if (!float.IsFinite(x))
            {
                buffer[index] = float.IsNaN(x) ? 0f : MathF.CopySign(ceiling, x);
                continue;
            }

            float absX = Math.Abs(x);

            if (absX <= threshold) continue;

            // 目标：将 (Threshold, ∞) 映射到 (Threshold, Ceiling)
            // 曲线特性：在 Threshold 处斜率为 1 (平滑过渡)，无穷大时趋向 Ceiling
            float over = absX - threshold;

            // 与 softRange * over / (softRange + over) 等价，但不会在极大输入下溢出或产生 ∞/∞。
            float soft = softRange - (softRange * softRange) / (softRange + over);

            float result = Math.Min(threshold + soft, ceiling);

            buffer[index] = MathF.CopySign(result, x);
        }
    }

    public void UpdateParameters(float threshold, float ceiling)
    {
        if (!float.IsFinite(threshold))
            throw new ArgumentOutOfRangeException(nameof(threshold), threshold, "Threshold must be finite.");
        if (!float.IsFinite(ceiling))
            throw new ArgumentOutOfRangeException(nameof(ceiling), ceiling, "Ceiling must be finite.");

        float normalizedCeiling = Math.Clamp(
            ceiling,
            MinimumThreshold + MinimumSoftRange,
            MaximumCeiling);
        float normalizedThreshold = Math.Clamp(
            threshold,
            MinimumThreshold,
            normalizedCeiling - MinimumSoftRange);

        Volatile.Write(
            ref _parameters,
            new Parameters(
                normalizedThreshold,
                normalizedCeiling,
                normalizedCeiling - normalizedThreshold));
    }

    public static RationalSoftClipper GamePreset(ISampleProvider sampleProvider)
    {
        return new RationalSoftClipper(sampleProvider);
    }

    private sealed record Parameters(float Threshold, float Ceiling, float SoftRange);
}
