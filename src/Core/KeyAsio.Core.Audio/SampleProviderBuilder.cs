using NAudio.Wave;

namespace KeyAsio.Core.Audio;

public class SampleProviderBuilder
{
    public SampleProviderBuilder(ISampleProvider sampleProvider)
    {
        CurrentSampleProvider = sampleProvider;
    }

    public ISampleProvider CurrentSampleProvider { get; private set; }

    public T AddSampleProvider<T>(Func<ISampleProvider, T> creation) where T : ISampleProvider
    {
        var sampleProvider = creation(CurrentSampleProvider);
        CurrentSampleProvider = sampleProvider;
        return sampleProvider;
    }
}