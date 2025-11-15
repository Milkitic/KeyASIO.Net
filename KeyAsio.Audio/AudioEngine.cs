using KeyAsio.Audio.Caching;
using KeyAsio.Audio.SampleProviders;
using Milki.Extensions.Threading;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace KeyAsio.Audio;

public class AudioEngine
{
    private readonly AudioDeviceManager _audioDeviceManager;
    private readonly AudioCacheManager _audioCacheManager;
    private readonly SynchronizationContext _context;

    private readonly EnhancedVolumeSampleProvider _effectVolumeSampleProvider = new(null);
    private readonly EnhancedVolumeSampleProvider _musicVolumeSampleProvider = new(null);

    public AudioEngine(AudioDeviceManager audioDeviceManager, AudioCacheManager audioCacheManager)
    {
        _audioDeviceManager = audioDeviceManager;
        _audioCacheManager = audioCacheManager;
        _context = SynchronizationContext.Current ?? new SingleSynchronizationContext("AudioPlaybackEngine_STA",
            staThread: true, threadPriority: ThreadPriority.AboveNormal);
    }

    public IWavePlayer? CurrentDevice { get; private set; }
    public WaveFormat SourceWaveFormat { get; private set; } = null!;
    public WaveFormat EngineWaveFormat { get; private set; } = null!;
    public MixingSampleProvider EffectMixer { get; private set; } = null!;
    public MixingSampleProvider MusicMixer { get; private set; } = null!;
    public MixingSampleProvider RootMixer { get; private set; } = null!;
    public ISampleProvider RootSampleProvider { get; private set; } = null!;

    public float EffectVolume
    {
        get => _effectVolumeSampleProvider.Volume;
        set => _effectVolumeSampleProvider.Volume = value;
    }

    public float MusicVolume
    {
        get => _musicVolumeSampleProvider.Volume;
        set => _musicVolumeSampleProvider.Volume = value;
    }

    public void StartDevice(DeviceDescription? deviceDescription, WaveFormat? waveFormat = null)
    {
        var (outputDevice, _) = _audioDeviceManager.CreateDevice(deviceDescription, _context);
        StartDevice(outputDevice, waveFormat);
    }

    public void StartDevice(IWavePlayer? outputDevice, WaveFormat? waveFormat = null)
    {
        waveFormat ??= new WaveFormat(44100, 2);
        EngineWaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(waveFormat.SampleRate, waveFormat.Channels);
        SourceWaveFormat = waveFormat;

        RootMixer = new MixingSampleProvider(EngineWaveFormat)
        {
            ReadFully = true
        };
        EffectMixer = new MixingSampleProvider(EngineWaveFormat)
        {
            ReadFully = true
        };
        _effectVolumeSampleProvider.Source = EffectMixer;

        MusicMixer = new MixingSampleProvider(EngineWaveFormat)
        {
            ReadFully = true
        };
        _musicVolumeSampleProvider.Source = MusicMixer;
        RootMixer.AddMixerInput(_effectVolumeSampleProvider);
        RootMixer.AddMixerInput(_musicVolumeSampleProvider);

        ISampleProvider root = RootMixer;

        if (outputDevice != null)
        {
            Exception? ex = null;
            _context.Send(_ =>
            {
                try
                {
                    outputDevice.Init(root);
                }
                catch (Exception e)
                {
                    ex = e;
                }
            }, null);

            if (ex != null) throw ex;
            outputDevice.Play();
        }

        CurrentDevice = outputDevice;
        RootSampleProvider = root;
    }

    public void StopDevice()
    {
        if (CurrentDevice == null) return;
        CurrentDevice.Dispose();
        _effectVolumeSampleProvider.Source = null;
        _musicVolumeSampleProvider.Source = null;
        CurrentDevice = null;
    }

    public void AddMixerInput(ISampleProvider input)
    {
        if (!RootMixer.MixerInputs.Contains(input))
        {
            RootMixer.AddMixerInput(input);
        }
    }

    public void RemoveMixerInput(ISampleProvider input)
    {
        if (RootMixer.MixerInputs.Contains(input))
        {
            RootMixer.RemoveMixerInput(input);
        }
    }

    public ISampleProvider? PlayAudio(CachedAudio cachedAudio, SampleControl? sampleControl = null)
    {
        var rootSample = RootMixer.PlayAudio(cachedAudio, sampleControl);
        return rootSample;
    }

    public ISampleProvider? PlayAudio(CachedAudio cachedAudio, float volume, float balance = 0)
    {
        var rootSample = RootMixer.PlayAudio(cachedAudio, balance, volume);
        return rootSample;
    }

    public async Task<ISampleProvider?> PlayAudio(string path, SampleControl? sampleControl = null)
    {
        var rootSample = await RootMixer.PlayAudio(_audioCacheManager, path, sampleControl).ConfigureAwait(false);
        return rootSample;
    }

    public async Task<ISampleProvider?> PlayAudio(string path, float volume, float balance = 0)
    {
        var rootSample = await RootMixer.PlayAudio(_audioCacheManager, path, balance, volume).ConfigureAwait(false);
        return rootSample;
    }
}