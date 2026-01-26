using System.ComponentModel;
using System.Runtime.CompilerServices;
using KeyAsio.Core.Audio.SampleProviders;
using KeyAsio.Core.Audio.SampleProviders.Limiters;
using KeyAsio.Core.Audio.Wave;
using Milki.Extensions.Threading;
using NAudio.Wave;

namespace KeyAsio.Core.Audio;

public class AudioEngine : IPlaybackEngine, INotifyPropertyChanged
{
    private readonly IAudioDeviceManager _audioDeviceManager;
    private SynchronizationContext? _context;

    private readonly EnhancedVolumeSampleProvider _effectVolumeSampleProvider = new(null) { ExcludeFromPool = true };
    private readonly EnhancedVolumeSampleProvider _musicVolumeSampleProvider = new(null) { ExcludeFromPool = true };
    private readonly EnhancedVolumeSampleProvider _mainVolumeSampleProvider = new(null) { ExcludeFromPool = true };
    private DynamicLimiterProvider? _limiterProvider;
    private LimiterType _limiterType = LimiterType.Master;

    public AudioEngine(IAudioDeviceManager audioDeviceManager)
    {
        _audioDeviceManager = audioDeviceManager;
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

    public LimiterType LimiterType
    {
        get => _limiterType;
        set
        {
            _limiterType = value;
            _limiterProvider?.UpdateLimiter(value);
        }
    }

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
            ReadFully = true,
            WantsKeep = true
        };
        _effectVolumeSampleProvider.Source = EffectMixer;

        MusicMixer = new QueueMixingSampleProvider(EngineWaveFormat)
        {
            ReadFully = true,
            WantsKeep = true
        };
        _musicVolumeSampleProvider.Source = MusicMixer;
        RootMixer.AddMixerInput(_effectVolumeSampleProvider);
        RootMixer.AddMixerInput(_musicVolumeSampleProvider);

        _mainVolumeSampleProvider.Source = RootMixer;
        _limiterProvider = new DynamicLimiterProvider(_mainVolumeSampleProvider, _limiterType);
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