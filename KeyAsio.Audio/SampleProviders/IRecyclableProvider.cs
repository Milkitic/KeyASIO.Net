using NAudio.Wave;

namespace KeyAsio.Audio.SampleProviders;

public interface IRecyclableProvider : ISampleProvider
{
    ISampleProvider? ResetAndGetSource();
}