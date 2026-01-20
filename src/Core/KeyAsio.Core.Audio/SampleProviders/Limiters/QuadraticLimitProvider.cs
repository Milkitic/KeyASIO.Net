using NAudio.Wave;

namespace KeyAsio.Core.Audio.SampleProviders.Limiters;

/// <summary>
/// Provides a simple, low-CPU limiter with a soft knee and a hard ceiling.
/// </summary>
/// <remarks>
/// This provider applies a simple compression (soft knee) to signals
/// exceeding the <see cref="Threshold"/> and then hard-clips any signal
/// that would exceed the <see cref="Ceiling"/>.
/// This is not a lookahead limiter and may cause distortion on fast transients,
/// but it is very lightweight.
/// </remarks>
public sealed class QuadraticLimitProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private float _threshold = 0.95f;
    private float _ceiling = 0.99f;
    private float _range;
    private float _fourRange;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuadraticLimitProvider"/> class.
    /// </summary>
    /// <param name="source">The source sample provider.</param>
    /// <param name="threshold">
    /// The linear amplitude threshold (0 to 1) at which compression begins.
    /// Default is 0.95.
    /// </param>
    /// <param name="ceiling">
    /// The maximum linear amplitude (0 to 1) for the output signal (hard clip).
    /// Default is 0.99.
    /// </param>
    public QuadraticLimitProvider(ISampleProvider source, float threshold = 0.95f, float ceiling = 0.99f)
    {
        _source = source;
        UpdateParameters(threshold, ceiling);
    }

    /// <summary>
    /// Gets the WaveFormat of this sample provider.
    /// </summary>
    public WaveFormat WaveFormat => _source.WaveFormat;

    /// <summary>
    /// Gets or sets the linear amplitude threshold (0.0 to 1.0) where compression starts.
    /// </summary>
    /// <value>The threshold, clamped between 0.0f and 1.0f.</value>
    public float Threshold
    {
        get => _threshold;
        set => _threshold = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>
    /// Gets or sets the linear amplitude ceiling (0.0 to 1.0).
    /// </summary>
    /// <value>The ceiling, clamped between 0.0f and 1.0f. This is the absolute maximum output level.</value>
    public float Ceiling
    {
        get => _ceiling;
        set => _ceiling = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>
    /// Reads samples from this provider, applying the simple limiting logic.
    /// </summary>
    /// <param name="buffer">The buffer to fill with samples.</param>
    /// <param name="offset">The offset into the buffer to start writing.</param>
    /// <param name="count">The number of samples requested.</param>
    /// <returns>The number of samples read.</returns>
    public int Read(float[] buffer, int offset, int count)
    {
        int samplesRead = _source.Read(buffer, offset, count);

        float threshold = _threshold;
        float ceiling = _ceiling;
        float fourRange = _fourRange;
        float twoRange = _range * 2.0f;

        for (int i = 0; i < samplesRead; i++)
        {
            int index = offset + i;
            float sample = buffer[index];
            float abs = Math.Abs(sample);

            if (abs <= threshold) continue;

            float x = abs - threshold;

            if (x < twoRange)
            {
                float soft = x - (x * x) / fourRange;
                buffer[index] = Math.Sign(sample) * (threshold + soft);
            }
            else
            {
                buffer[index] = Math.Sign(sample) * ceiling;
            }
        }

        return samplesRead;
    }

    private void UpdateParameters(float threshold, float ceiling)
    {
        // 确保参数合理
        _ceiling = Math.Clamp(ceiling, 0.1f, 1.0f);
        // Threshold 必须小于 Ceiling，否则就没有软拐点空间了
        _threshold = Math.Clamp(threshold, 0.0f, _ceiling - 0.001f);

        _range = _ceiling - _threshold;
        _fourRange = 4.0f * _range;
    }

    public static QuadraticLimitProvider GamePreset(ISampleProvider sampleProvider)
    {
        return new QuadraticLimitProvider(sampleProvider, 0.85f, 0.98f);
    }
}