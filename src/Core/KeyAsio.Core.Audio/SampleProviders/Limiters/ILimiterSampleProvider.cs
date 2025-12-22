using NAudio.Wave;

namespace KeyAsio.Core.Audio.SampleProviders.Limiters;

public interface ILimiterSampleProvider : ISampleProvider
{
    bool IsEnabled { get; set; }
}