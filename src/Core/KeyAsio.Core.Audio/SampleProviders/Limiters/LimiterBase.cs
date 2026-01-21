using NAudio.Wave;

namespace KeyAsio.Core.Audio.SampleProviders.Limiters;

/// <summary>
/// Base class for limiter implementations.
/// Handles the common ISampleProvider boilerplate.
/// </summary>
public abstract class LimiterBase : ISampleProvider
{
    protected readonly ISampleProvider Source;

    protected LimiterBase(ISampleProvider source)
    {
        Source = source;
    }

    public WaveFormat WaveFormat => Source.WaveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        int samplesRead = Source.Read(buffer, offset, count);
        if (samplesRead == 0) return 0;

        Process(buffer, offset, samplesRead);
        return samplesRead;
    }

    /// <summary>
    /// Processes the audio buffer in-place.
    /// </summary>
    /// <param name="buffer">The buffer containing audio samples.</param>
    /// <param name="offset">The offset to start processing.</param>
    /// <param name="count">The number of samples to process.</param>
    protected abstract void Process(float[] buffer, int offset, int count);
}