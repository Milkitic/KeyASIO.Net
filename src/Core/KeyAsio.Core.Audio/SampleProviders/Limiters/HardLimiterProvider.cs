using NAudio.Wave;

namespace KeyAsio.Core.Audio.SampleProviders.Limiters;

/// <summary>
/// A hard clipping limiter that chops off signals exceeding the ceiling.
/// </summary>
/// <remarks>
/// This is the most basic form of limiting.
/// Pros: Zero CPU usage, Zero latency, Perfectly transparent below threshold.
/// Cons: Introduces harsh digital distortion (aliasing) when limiting occurs.
/// Best used as a safety net where limiting is rarely expected to happen.
/// </remarks>
public sealed class HardLimiterProvider : LimiterBase
{
    private float _ceiling = 1.0f;

    public HardLimiterProvider(ISampleProvider source, float ceiling = 0.99f) : base(source)
    {
        Ceiling = ceiling;
    }

    public float Ceiling
    {
        get => _ceiling;
        set => _ceiling = Math.Clamp(value, 0.1f, 1.0f);
    }

    protected override void Process(float[] buffer, int offset, int count)
    {
        float ceiling = _ceiling;

        for (int i = 0; i < count; i++)
        {
            int index = offset + i;
            float sample = buffer[index];

            if (sample > ceiling)
            {
                buffer[index] = ceiling;
            }
            else if (sample < -ceiling)
            {
                buffer[index] = -ceiling;
            }
        }
    }
}