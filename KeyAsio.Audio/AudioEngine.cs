using KeyAsio.Audio.Caching;
using KeyAsio.Audio.SampleProviders;
using KeyAsio.Audio.SampleProviders.Limiters;
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
    private readonly EnhancedVolumeSampleProvider _mainVolumeSampleProvider = new(null);
    private MasterLimiterProvider? _limiterProvider;
    private bool _enableLimiter = true;

    public AudioEngine(AudioDeviceManager audioDeviceManager, AudioCacheManager audioCacheManager)
    {
        _audioDeviceManager = audioDeviceManager;
        _audioCacheManager = audioCacheManager;
        _context = SynchronizationContext.Current ?? new SingleSynchronizationContext("AudioPlaybackEngine_STA",
            staThread: true, threadPriority: ThreadPriority.AboveNormal);
    }

    public bool EnableLimiter
    {
        get => _limiterProvider?.IsEnabled ?? _enableLimiter;
        set
        {
            _limiterProvider?.IsEnabled = value;
            _enableLimiter = value;
        }
    }

    public IWavePlayer? CurrentDevice { get; private set; }
    public WaveFormat SourceWaveFormat { get; private set; } = null!;
    public WaveFormat EngineWaveFormat { get; private set; } = null!;
    public MixingSampleProvider EffectMixer { get; private set; } = null!;
    public MixingSampleProvider MusicMixer { get; private set; } = null!;
    public MixingSampleProvider RootMixer { get; private set; } = null!;
    public ISampleProvider RootSampleProvider { get; private set; } = null!;

    public float MainVolume
    {
        get => _mainVolumeSampleProvider.Volume;
        set => _mainVolumeSampleProvider.Volume = value;
    }

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

        _mainVolumeSampleProvider.Source = RootMixer;
        _limiterProvider = new MasterLimiterProvider(_mainVolumeSampleProvider,
                thresholdDb: -0.5f, // -0.5dB 触发
                ceilingDb: -0.1f, // -0.1dB 上限
                attackMs: 0.1f, // 0.1ms 快速响应
                releaseMs: 50f, // 50ms 释放
                lookaheadMs: 2f) // 2ms 前瞻
        {
            IsEnabled = _enableLimiter
        };
        //var root = _limiterProvider = new SoftLimiterProvider(RootMixer,
        //    drive: 0.9f);
        //var root = _limiterProvider = new SimpleLimiterProvider(RootMixer);
        ISampleProvider root = _limiterProvider;
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
        _limiterProvider = null;
        _effectVolumeSampleProvider.Source = null;
        _musicVolumeSampleProvider.Source = null;
        _mainVolumeSampleProvider.Source = null;
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