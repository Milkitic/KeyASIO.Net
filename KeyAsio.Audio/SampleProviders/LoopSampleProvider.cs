using NAudio.Wave;

namespace KeyAsio.Audio.SampleProviders;

public class LoopSampleProvider : ISampleProvider
{
    private readonly SeekableCachedAudioProvider _source;

    public LoopSampleProvider(SeekableCachedAudioProvider source)
    {
        _source = source;
    }

    public bool EnableLooping { get; set; } = true;

    public WaveFormat WaveFormat => _source.WaveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        int totalSamplesRead = 0;

        while (totalSamplesRead < count)
        {
            int samplesRequired = count - totalSamplesRead;
            int samplesRead = _source.Read(buffer, offset + totalSamplesRead, samplesRequired);

            if (samplesRead == 0)
            {
                if (!EnableLooping)
                {
                    break;
                }

                if (_source.PlayTime == TimeSpan.Zero)
                {
                    // Something wrong with the source stream
                    break;
                }

                // Begin loop
                _source.PlayTime = TimeSpan.Zero;
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
}