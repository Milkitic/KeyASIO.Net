using NAudio.Wave;

namespace KeyAsio.Audio.SampleProviders.Limiters;

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
public class SimpleLimiterProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private float _threshold = 0.95f;
    private float _ceiling = 0.99f;

    /// <summary>
    /// Initializes a new instance of the <see cref="SimpleLimiterProvider"/> class.
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
    public SimpleLimiterProvider(ISampleProvider source, float threshold = 0.95f, float ceiling = 0.99f)
    {
        _source = source;
        _threshold = threshold;
        _ceiling = ceiling;
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