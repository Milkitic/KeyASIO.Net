using System.Numerics.Tensors;
using System.Runtime.CompilerServices;
using NAudio.Wave;

namespace KeyAsio.Audio.SampleProviders;

/// <summary>
/// Very simple sample provider supporting adjustable gain
/// </summary>
public class EnhancedVolumeSampleProvider : ISampleProvider
{
    private const float VolumeTolerance = 0.001f;

    /// <summary>
    /// Initializes a new instance of VolumeSampleProvider
    /// </summary>
    /// <param name="source">Source Sample Provider</param>
    public EnhancedVolumeSampleProvider(ISampleProvider? source)
    {
        Source = source;
        Volume = 1.0f;
    }

    /// <summary>
    /// Source Sample Provider
    /// </summary>
    public ISampleProvider? Source { get; set; }

    /// <summary>
    /// Allows adjusting the volume, 1.0f = full volume
    /// </summary>
    public float Volume { get; set; }

    /// <summary>
    /// WaveFormat
    /// </summary>
    public WaveFormat WaveFormat => Source?.WaveFormat ?? throw new InvalidOperationException("Source not ready");

    /// <summary>
    /// Reads samples from this sample provider
    /// </summary>
    /// <param name="buffer">Sample buffer</param>
    /// <param name="offset">Offset into sample buffer</param>
    /// <param name="sampleCount">Number of samples desired</param>
    /// <returns>Number of samples read</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Read(float[] buffer, int offset, int sampleCount)
    {
        if (Source == null)
        {
            Array.Clear(buffer, offset, sampleCount);
            return sampleCount;
        }

        int samplesRead = Source.Read(buffer, offset, sampleCount);

        float currentVolume = Volume;
        if (Math.Abs(currentVolume - 1f) <= VolumeTolerance)
        {
            return samplesRead;
        }

        Span<float> span = buffer.AsSpan(offset, samplesRead);
        TensorPrimitives.Multiply(span, currentVolume, span);

        return samplesRead;
    }
}