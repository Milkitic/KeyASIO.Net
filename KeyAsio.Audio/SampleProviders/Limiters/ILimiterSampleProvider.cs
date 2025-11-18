using NAudio.Wave;

namespace KeyAsio.Audio.SampleProviders.Limiters;

public interface ILimiterSampleProvider : ISampleProvider
{
    bool IsEnabled { get; set; }
}