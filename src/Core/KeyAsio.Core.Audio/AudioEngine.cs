using System.ComponentModel;
using System.Runtime.CompilerServices;
using KeyAsio.Core.Audio.SampleProviders;
using KeyAsio.Core.Audio.SampleProviders.Limiters;
using KeyAsio.Core.Audio.Wave;
using Microsoft.Extensions.Logging;
using Milki.Extensions.Threading;
using NAudio.Wave;

namespace KeyAsio.Core.Audio;

public class AudioEngine : IPlaybackEngine, INotifyPropertyChanged
{
    private readonly IAudioDeviceManager _audioDeviceManager;
    private readonly ILogger<AudioEngine> _logger;
    private readonly Lock _deviceLock = new();
    private SynchronizationContext? _context;

    private readonly EnhancedVolumeSampleProvider _effectVolumeSampleProvider = new(null) { ExcludeFromPool = true };
    private readonly EnhancedVolumeSampleProvider _musicVolumeSampleProvider = new(null) { ExcludeFromPool = true };
    private readonly EnhancedVolumeSampleProvider _mainVolumeSampleProvider = new(null) { ExcludeFromPool = true };
    private DynamicLimiterProvider? _limiterProvider;
    private LimiterType _limiterType = LimiterType.Master;

    public event Action<DeviceDescription>? DeviceStarted;
    public event Action? DeviceStopped;
    public event Action<Exception>? DeviceError;

    public AudioEngine(IAudioDeviceManager audioDeviceManager, ILogger<AudioEngine>? logger = null)
    {
        _audioDeviceManager = audioDeviceManager;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<AudioEngine>.Instance;
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
        try
        {
            lock (_deviceLock)
            {
                StartDeviceCore(deviceDescription, waveFormat);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while starting audio device.");
            DeviceError?.Invoke(ex);
            throw;
        }
    }

    private void StartDeviceCore(DeviceDescription? deviceDescription, WaveFormat? waveFormat = null)
    {
        waveFormat ??= new WaveFormat(44100, 2);

        if (CurrentDevice != null &&
            SourceWaveFormat != null &&
            SourceWaveFormat.SampleRate == waveFormat.SampleRate &&
            SourceWaveFormat.Channels == waveFormat.Channels &&
            DeviceComparer.AreSettingsEqual(deviceDescription, CurrentDeviceDescription))
        {
            return;
        }

        if (CurrentDevice != null)
        {
            StopDeviceCore();
        }

        _context = SynchronizationContext.Current ?? new SingleSynchronizationContext("AudioPlaybackEngine_STA",
            staThread: true, threadPriority: ThreadPriority.AboveNormal);

        var creationDeviceDescription = PrepareDeviceDescriptionForCreation(deviceDescription, waveFormat);
        var (outputDevice, actualDescription) = _audioDeviceManager.CreateDevice(creationDeviceDescription, _context);

        var newWaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(waveFormat.SampleRate, waveFormat.Channels);
        bool waveFormatChanged = EngineWaveFormat == null! ||
                                 EngineWaveFormat.SampleRate != newWaveFormat.SampleRate ||
                                 EngineWaveFormat.Channels != newWaveFormat.Channels;

        SourceWaveFormat = waveFormat;
        EngineWaveFormat = newWaveFormat;

        if (waveFormatChanged)
        {
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
        }
        else
        {
            _effectVolumeSampleProvider.Source = EffectMixer;
            _musicVolumeSampleProvider.Source = MusicMixer;
            _mainVolumeSampleProvider.Source = RootMixer;
            if (_limiterProvider == null)
            {
                _limiterProvider = new DynamicLimiterProvider(_mainVolumeSampleProvider, _limiterType);
            }
        }

        RootSampleProvider = _limiterProvider;

        ISampleProvider root = RootSampleProvider;

        Exception? ex = null;
        _context.Send(_ =>
        {
            try
            {
                outputDevice.Init(new PerfSampleToWaveProvider(root));
                outputDevice.Play();
            }
            catch (Exception e)
            {
                ex = e;
            }
        }, null);

        if (ex != null)
        {
            DisposeDevice(outputDevice);
            throw ex;
        }

        CurrentDevice = outputDevice;
        CurrentDeviceDescription = RestoreDeviceDescriptionForState(deviceDescription, actualDescription);

        if (outputDevice is AsioOut asioOut)
        {
            asioOut.DriverResetRequest += AsioOut_DriverResetRequest;
        }

        DeviceStarted?.Invoke(CurrentDeviceDescription);
    }

    /// <summary>
    /// Hook for subclasses to rewrite the device description before it is handed to
    /// the <see cref="IAudioDeviceManager"/>. The default implementation is a no-op;
    /// a subclass can, for example, convert latency from milliseconds to buffer
    /// frames for a backend that requires the latter.
    /// </summary>
    protected virtual DeviceDescription? PrepareDeviceDescriptionForCreation(
        DeviceDescription? description,
        WaveFormat waveFormat)
    {
        return description;
    }

    /// <summary>
    /// Hook for subclasses to adjust the post-creation description that becomes the
    /// engine's <see cref="CurrentDeviceDescription"/>. The default implementation
    /// returns <paramref name="actualDescription"/> as-is; a subclass can, for
    /// example, restore the user-facing latency in milliseconds after the backend
    /// has reported a buffer-frame value.
    /// </summary>
    protected virtual DeviceDescription RestoreDeviceDescriptionForState(
        DeviceDescription? configuredDescription,
        DeviceDescription actualDescription)
    {
        return actualDescription;
    }

    public void StopDevice()
    {
        lock (_deviceLock)
        {
            StopDeviceCore();
        }
    }

    private void StopDeviceCore()
    {
        if (CurrentDevice == null) return;
        var currentDevice = CurrentDevice;

        CurrentDevice = null;
        CurrentDeviceDescription = null;

        _limiterProvider = null;
        _effectVolumeSampleProvider.Source = null;
        _musicVolumeSampleProvider.Source = null;
        _mainVolumeSampleProvider.Source = null;

        DisposeDevice(currentDevice);
        DeviceStopped?.Invoke();
    }

    private void DisposeDevice(IWavePlayer device)
    {
        if (device is AsioOut asioOut)
        {
            asioOut.DriverResetRequest -= AsioOut_DriverResetRequest;
        }

        device.Dispose();
    }

    private void AsioOut_DriverResetRequest(object? sender, EventArgs e)
    {
        _logger.LogWarning("ASIO driver requested reset. Re-initializing audio device...");
        _context?.Post(_ =>
        {
            try
            {
                lock (_deviceLock)
                {
                    var desc = CurrentDeviceDescription;
                    var format = SourceWaveFormat;
                    if (desc == null) return;

                    StopDeviceCore();
                    StartDeviceCore(desc, format);
                }

                _logger.LogInformation("ASIO driver reset completed successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to re-initialize ASIO device after driver reset request.");
                DeviceError?.Invoke(ex);
            }
        }, null);
    }

    WaveFormat? IMusicPlaybackSink.WaveFormat => MusicMixer?.WaveFormat;

    public void AddInput(ISampleProvider input)
    {
        MusicMixer.AddMixerInput(input);
    }

    public void RemoveInput(ISampleProvider input)
    {
        MusicMixer.RemoveMixerInput(input);
    }

    public void Dispose()
    {
        StopDevice();
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
