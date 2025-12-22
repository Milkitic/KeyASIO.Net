using NAudio.Wave;

namespace KeyAsio.Audio.SampleProviders;

public interface IMixingSampleProvider : ISampleProvider
{
    public bool ReadFully { get; set; }
    public void AddMixerInput(ISampleProvider mixerInput);
    public void RemoveMixerInput(ISampleProvider mixerInput);
    public void RemoveAllMixerInputs();
}