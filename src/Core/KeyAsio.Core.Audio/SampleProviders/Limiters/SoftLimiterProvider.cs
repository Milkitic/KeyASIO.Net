using NAudio.Wave;

namespace KeyAsio.Core.Audio.SampleProviders.Limiters;

/// <summary>
/// Provides a soft limiting effect using the hyperbolic tangent (tanh) function,
/// simulating analog saturation.
/// </summary>
/// <remarks>
/// This provider applies soft clipping to the signal, gently rounding off peaks
/// rather than hard-clipping them. This can add perceived warmth and prevent
/// harsh digital distortion. The <see cref="Drive"/> parameter controls the
/// intensity of the saturation effect.
/// </remarks>
public sealed class SoftLimiterProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private float _drive = 0.9f;  // 驱动强度

    /// <summary>
    /// Initializes a new instance of the <see cref="SoftLimiterProvider"/> class.
    /// </summary>
    /// <param name="source">The source sample provider to apply the effect to.</param>
    /// <param name="drive">
    /// The drive amount, typically between 0.5 and 1.0. Higher values increase
    /// the saturation. Default is 0.9.
    /// </param>
    public SoftLimiterProvider(ISampleProvider source, float drive = 0.9f)
    {
        _source = source;
        _drive = drive;
    }

    /// <summary>
    /// Gets the WaveFormat of this sample provider.
    /// </summary>
    public WaveFormat WaveFormat => _source.WaveFormat;

    /// <summary>
    /// Gets or sets the drive amount, controlling the intensity of the saturation.
    /// </summary>
    /// <value>The drive amount. The value is clamped between 0.5f and 1.0f.</value>
    public float Drive
    {
        get => _drive;
        set => _drive = Math.Clamp(value, 0.5f, 1.0f);
    }

    /// <summary>
    /// Reads samples from this provider, applying the tanh soft limiting.
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
            // tanh 软饱和 - 模拟模拟设备的自然限制
            buffer[index] = MathF.Tanh(buffer[index] * _drive) / MathF.Tanh(_drive);
        }

        return samplesRead;
    }
}