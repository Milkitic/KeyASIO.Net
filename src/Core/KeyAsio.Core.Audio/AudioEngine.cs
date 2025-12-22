using System.ComponentModel;
using System.Runtime.CompilerServices;
using KeyAsio.Core.Audio.Caching;
using KeyAsio.Core.Audio.SampleProviders;
using KeyAsio.Core.Audio.SampleProviders.Limiters;
using KeyAsio.Core.Audio.Wave;
using Milki.Extensions.Threading;
using NAudio.Wave;

namespace KeyAsio.Core.Audio;

public class AudioEngine : IDisposable, INotifyPropertyChanged
{
    private readonly AudioDeviceManager _audioDeviceManager;
    private readonly AudioCacheManager _audioCacheManager;
    private SynchronizationContext? _context;

    private readonly EnhancedVolumeSampleProvider _effectVolumeSampleProvider = new(null) { ExcludeFromPool = true };
    private readonly EnhancedVolumeSampleProvider _musicVolumeSampleProvider = new(null) { ExcludeFromPool = true };
    private readonly EnhancedVolumeSampleProvider _mainVolumeSampleProvider = new(null) { ExcludeFromPool = true };
    private ILimiterSampleProvider? _limiterProvider;
    private bool _enableLimiter = true;

    public AudioEngine(AudioDeviceManager audioDeviceManager, AudioCacheManager audioCacheManager)
    {
        _audioDeviceManager = audioDeviceManager;
        _audioCacheManager = audioCacheManager;
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

    public DeviceDescription? CurrentDeviceDescription
    {
        get;
        private set => SetField(ref field, value);
    }

    public WaveFormat EngineWaveFormat
    {
        get;
        private set => SetField(ref field, value);
    } = null!;

    public WaveFormat SourceWaveFormat { get; private set; } = null!;

    public IMixingSampleProvider EffectMixer { get; private set; } = null!;
    public IMixingSampleProvider MusicMixer { get; private set; } = null!;
    public IMixingSampleProvider RootMixer { get; private set; } = null!;
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
        _context = SynchronizationContext.Current ?? new SingleSynchronizationContext("AudioPlaybackEngine_STA",
            staThread: true, threadPriority: ThreadPriority.AboveNormal);

        var (outputDevice, actualDescription) = _audioDeviceManager.CreateDevice(deviceDescription, _context);

        waveFormat ??= new WaveFormat(44100, 2);
        SourceWaveFormat = waveFormat;
        EngineWaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(waveFormat.SampleRate, waveFormat.Channels);

        RootMixer = new QueueMixingSampleProvider(EngineWaveFormat)
        {
            ReadFully = true
        };
        EffectMixer = new QueueMixingSampleProvider(EngineWaveFormat)
        {
            ReadFully = true
        };
        _effectVolumeSampleProvider.Source = EffectMixer;

        MusicMixer = new QueueMixingSampleProvider(EngineWaveFormat)
        {
            ReadFully = true
        };
        _musicVolumeSampleProvider.Source = MusicMixer;
        RootMixer.AddMixerInput(_effectVolumeSampleProvider);
        RootMixer.AddMixerInput(_musicVolumeSampleProvider);

        _mainVolumeSampleProvider.Source = RootMixer;
        _limiterProvider = MasterLimiterProvider.UltraLowLatencyPreset(_mainVolumeSampleProvider);
        _limiterProvider.IsEnabled = _enableLimiter;
        ISampleProvider root = _limiterProvider;

        Exception? ex = null;
        _context.Send(_ =>
        {
            try
            {
                outputDevice.Init(new PerfSampleToWaveProvider(root));
            }
            catch (Exception e)
            {
                ex = e;
            }
        }, null);

        if (ex != null) throw ex;
        outputDevice.Play();

        CurrentDevice = outputDevice;
        CurrentDeviceDescription = actualDescription;
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
        CurrentDeviceDescription = null;
    }

    [Obsolete]
    public ISampleProvider? PlayAudio(CachedAudio cachedAudio, SampleControl? sampleControl = null)
    {
        var rootSample = RootMixer.PlayAudio(cachedAudio, sampleControl);
        return rootSample;
    }

    [Obsolete]
    public ISampleProvider? PlayAudio(CachedAudio cachedAudio, float volume, float balance = 0)
    {
        var rootSample = RootMixer.PlayAudio(cachedAudio, balance, volume);
        return rootSample;
    }

    [Obsolete]
    public async Task<ISampleProvider?> PlayAudio(string path, SampleControl? sampleControl = null)
    {
        var rootSample = await RootMixer.PlayAudio(_audioCacheManager, path, sampleControl).ConfigureAwait(false);
        return rootSample;
    }

    [Obsolete]
    public async Task<ISampleProvider?> PlayAudio(string path, float volume, float balance = 0)
    {
        var rootSample = await RootMixer.PlayAudio(_audioCacheManager, path, balance, volume).ConfigureAwait(false);
        return rootSample;
    }

    public void Dispose()
    {
        CurrentDevice?.Dispose();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}