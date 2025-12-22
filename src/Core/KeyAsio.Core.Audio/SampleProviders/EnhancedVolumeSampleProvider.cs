using System.Numerics.Tensors;
using System.Runtime.CompilerServices;
using KeyAsio.Audio.Utils;
using NAudio.Wave;

namespace KeyAsio.Audio.SampleProviders;

/// <summary>
/// Very simple sample provider supporting adjustable gain
/// </summary>
public sealed class EnhancedVolumeSampleProvider : IRecyclableProvider, IPoolable
{
    private const float VolumeTolerance = 0.001f;

    public EnhancedVolumeSampleProvider()
    {
    }

    /// <summary>
    /// Initializes a new instance of VolumeSampleProvider
    /// </summary>
    /// <param name="source">Source Sample Provider</param>
    public EnhancedVolumeSampleProvider(ISampleProvider? source)
    {
        Source = source;
    }

    /// <summary>
    /// Source Sample Provider
    /// </summary>
    public ISampleProvider? Source { get; set; }

    /// <summary>
    /// Allows adjusting the volume, 1.0f = full volume
    /// </summary>
    public float Volume { get; set; } = 1.0f;

    /// <summary>
    /// WaveFormat
    /// </summary>
    public WaveFormat WaveFormat => Source?.WaveFormat ?? throw new InvalidOperationException("Source not ready");

    public ISampleProvider? ResetAndGetSource()
    {
        var child = Source;
        Reset();
        return child;
    }

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

        if (sampleCount == 0) return 0;
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        Source = null;
        Volume = 1.0f;
    }

    public bool ExcludeFromPool { get; init; }
}