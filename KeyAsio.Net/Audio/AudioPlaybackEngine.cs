using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace KeyAsio.Net.Audio;

public class AudioPlaybackEngine : IDisposable
{
    private readonly MixingSampleProvider _mixer;
    private readonly IWavePlayer _outputDevice;

    public AudioPlaybackEngine(IWavePlayer outputDevice, int sampleRate = 44100, int channelCount = 2)
    {
        _outputDevice = outputDevice;
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channelCount);
        _mixer = new MixingSampleProvider(WaveFormat) { ReadFully = true };
        _outputDevice.Init(_mixer);
        _outputDevice.Play();
    }

    public WaveFormat WaveFormat { get; }

    public void PlaySound(CachedSound? sound)
    {
        if (sound == null) return;
        AddMixerInput(new CachedSoundSampleProvider(sound));
    }

    public void Dispose()
    {
        _outputDevice?.Dispose();
    }

    private void AddMixerInput(ISampleProvider input)
    {
        _mixer.AddMixerInput(input);
    }
}