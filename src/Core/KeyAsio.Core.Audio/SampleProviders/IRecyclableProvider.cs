using NAudio.Wave;

namespace KeyAsio.Core.Audio.SampleProviders;

public interface IRecyclableProvider : ISampleProvider
{
    ISampleProvider? ResetAndGetSource();
}