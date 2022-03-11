using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace KeyAsio.Net.Audio;

public class AudioPlaybackEngine
{
    private readonly MixingSampleProvider _mixer;
    private readonly IWavePlayer _outputDevice;

    public AudioPlaybackEngine(IWavePlayer outputDevice, int sampleRate = 44100, int channelCount = 2)
    {
        _outputDevice = outputDevice;
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channelCount);
        _mixer = new MixingSampleProvider(WaveFormat) { ReadFully = true };
    }

    public void Start()
    {
        _outputDevice.Init(_mixer);
        _outputDevice.Play();
    }

    public WaveFormat WaveFormat { get; }

    public void PlaySound(CachedSound? sound)
    {
        if (sound == null) return;
        AddMixerInput(new CachedSoundSampleProvider(sound));
    }

    private void AddMixerInput(ISampleProvider input)
    {
        _mixer.AddMixerInput(input);
    }
}