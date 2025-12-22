using KeyAsio.Core.Audio;
using KeyAsio.Plugins.Abstractions;
using NAudio.Wave;

namespace KeyAsio.Shared.Plugins;

public class AudioEngineWrapper : IAudioEngine
{
    private readonly AudioEngine _engine;

    public AudioEngineWrapper(AudioEngine engine)
    {
        _engine = engine;
    }

    public ISampleProvider MainMixer => _engine.RootMixer;
    public ISampleProvider EffectMixer => _engine.EffectMixer;
    public ISampleProvider MusicMixer => _engine.MusicMixer;
    public WaveFormat EngineWaveFormat => _engine.EngineWaveFormat;

    public void AddMixerInput(ISampleProvider input)
    {
        _engine.RootMixer.AddMixerInput(input);
    }

    public void RemoveMixerInput(ISampleProvider input)
    {
        _engine.RootMixer.RemoveMixerInput(input);
    }
}