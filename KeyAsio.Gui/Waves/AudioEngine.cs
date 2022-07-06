using Milki.Extensions.MixPlayer.Devices;
using Milki.Extensions.MixPlayer.NAudioExtensions;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace KeyAsio.Gui.Waves;

public class AudioEngine : AudioPlaybackEngine
{
    private VolumeSampleProvider _hitsoundVolumeSampleProvider = null!;
    private VolumeSampleProvider _musicVolumeSampleProvider = null!;

    public AudioEngine(IWavePlayer? outputDevice, int sampleRate = 44100, int channelCount = 2,
        bool notifyProgress = true, bool enableVolume = true)
        : base(outputDevice, sampleRate, channelCount, notifyProgress, enableVolume)
    {
        InitializeMixers();
    }

    public AudioEngine(DeviceDescription? deviceDescription, int sampleRate = 44100, int channelCount = 2,
        bool notifyProgress = true, bool enableVolume = true)
        : base(deviceDescription, sampleRate, channelCount, notifyProgress, enableVolume)
    {
        InitializeMixers();
    }

    public MixingSampleProvider EffectMixer { get; private set; } = null!;
    public MixingSampleProvider MusicMixer { get; private set; } = null!;

    public float EffectVolume
    {
        get => _hitsoundVolumeSampleProvider.Volume;
        set
        {
            if (value.Equals(_hitsoundVolumeSampleProvider.Volume)) return;
            _hitsoundVolumeSampleProvider.Volume = value;
            OnPropertyChanged();
        }
    }

    public float MusicVolume
    {
        get => _musicVolumeSampleProvider.Volume;
        set
        {
            if (value.Equals(_musicVolumeSampleProvider.Volume)) return;
            _musicVolumeSampleProvider.Volume = value;
            OnPropertyChanged();
        }
    }

    private void InitializeMixers()
    {
        EffectMixer = new MixingSampleProvider(WaveFormat)
        {
            ReadFully = true
        };
        _hitsoundVolumeSampleProvider = new VolumeSampleProvider(EffectMixer)
        {
            Volume = 1f
        };
        RootMixer.AddMixerInput(_hitsoundVolumeSampleProvider);

        MusicMixer = new MixingSampleProvider(WaveFormat)
        {
            ReadFully = true
        };
        _musicVolumeSampleProvider = new VolumeSampleProvider(MusicMixer)
        {
            Volume = 1f
        };
        RootMixer.AddMixerInput(_musicVolumeSampleProvider);
    }
}