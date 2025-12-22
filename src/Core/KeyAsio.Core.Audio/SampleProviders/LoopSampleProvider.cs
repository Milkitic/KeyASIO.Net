using KeyAsio.Audio.Utils;
using NAudio.Wave;

namespace KeyAsio.Audio.SampleProviders;

public sealed class LoopSampleProvider : IRecyclableProvider, IPoolable
{
    public LoopSampleProvider()
    {
    }

    public LoopSampleProvider(CachedAudioProvider source)
    {
        Source = source;
    }

    public CachedAudioProvider? Source { get; set; }

    public bool EnableLooping { get; set; } = true;

    public WaveFormat WaveFormat => Source?.WaveFormat ?? throw new InvalidOperationException("Source not initialized");

    public ISampleProvider? ResetAndGetSource()
    {
        var child = Source;
        Reset();
        return child;
    }

    public int Read(float[] buffer, int offset, int count)
    {
        if (Source == null)
        {
            Array.Clear(buffer, offset, count);
            return count;
        }

        int totalSamplesRead = 0;

        while (totalSamplesRead < count)
        {
            int samplesRequired = count - totalSamplesRead;
            int samplesRead = Source.Read(buffer, offset + totalSamplesRead, samplesRequired);

            if (samplesRead == 0)
            {
                if (!EnableLooping)
                {
                    break;
                }

                if (Source.PlayTime == TimeSpan.Zero)
                {
                    // Something wrong with the source stream
                    break;
                }

                // Begin loop
                Source.PlayTime = TimeSpan.Zero;
            }

            totalSamplesRead += samplesRead;
        }

        // In case of the edge case, clear the remaining buffer
        if (totalSamplesRead < count)
        {
            Array.Clear(buffer, offset + totalSamplesRead, count - totalSamplesRead);
        }

        return totalSamplesRead;
    }

    public void Reset()
    {
        Source = null;
        EnableLooping = true;
    }

    public bool ExcludeFromPool { get; init; }
}