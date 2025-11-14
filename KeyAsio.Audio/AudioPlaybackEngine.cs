using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using KeyAsio.Audio.Caching;
using KeyAsio.Audio.SampleProviders;
using Milki.Extensions.Threading;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace KeyAsio.Audio;

public class SampleControl
{
    internal Action<float>? VolumeChanged { get; set; }
    internal Action<float>? BalanceChanged { get; set; }

    private float _volume = 1;
    private float _balance = 0;

    public float Volume
    {
        get => _volume;
        set
        {
            if (Equals(_volume, value)) return;
            _volume = value;
            VolumeChanged?.Invoke(value);
        }
    }

    public float Balance
    {
        get => _balance;
        set
        {
            if (Equals(_balance, value)) return;
            _balance = value;
            BalanceChanged?.Invoke(value);
        }
    }
}

internal static class MixingSampleProviderExtension
{
    internal static ISampleProvider? PlayAudio(this MixingSampleProvider mixer, CachedAudio cachedAudio,
        SampleControl? sampleControl)
    {
        PlayAudio(mixer, cachedAudio, sampleControl, out var rootSample);
        return rootSample;
    }

    internal static ISampleProvider? PlayAudio(this MixingSampleProvider mixer, CachedAudio cachedAudio, float volume,
        float balance)
    {
        PlayAudio(mixer, cachedAudio, volume, balance, out var rootSample);
        return rootSample;
    }

    public static async Task<ISampleProvider?> PlayAudio(this MixingSampleProvider mixer, CachedAudioFactory factory,
        string path,
        SampleControl? sampleControl)
    {
        var waveFormat = new WaveFormat(mixer.WaveFormat.SampleRate, mixer.WaveFormat.Channels);
        await using var fs = File.OpenRead(path);
        var cacheResult = await factory.GetOrCreateOrEmpty(path, fs, waveFormat).ConfigureAwait(false);

        PlayAudio(mixer, cacheResult.CachedAudio!, sampleControl, out var rootSample);
        return rootSample;
    }

    public static async Task<ISampleProvider?> PlayAudio(this MixingSampleProvider mixer, CachedAudioFactory factory,
        string path,
        float volume, float balance)
    {
        var waveFormat = new WaveFormat(mixer.WaveFormat.SampleRate, mixer.WaveFormat.Channels);
        await using var fs = File.OpenRead(path);
        var cacheResult = await factory.GetOrCreateOrEmpty(path, fs, waveFormat).ConfigureAwait(false);

        PlayAudio(mixer, cacheResult.CachedAudio!, volume, balance, out var rootSample);
        return rootSample;
    }

    public static void AddMixerInput(this MixingSampleProvider mixer, ISampleProvider input,
        SampleControl? sampleControl, out ISampleProvider rootSample)
    {
        if (sampleControl != null)
        {
            var adjustVolume = input.AddToAdjustVolume(sampleControl.Volume);
            var adjustBalance = adjustVolume.AddToBalanceProvider(sampleControl.Balance);
            sampleControl.VolumeChanged ??= f => adjustVolume.Volume = f;
            sampleControl.BalanceChanged ??= f => adjustBalance.Balance = f;
            rootSample = adjustBalance;
            mixer.AddMixerInput(adjustBalance);
        }
        else
        {
            rootSample = input;
            mixer.AddMixerInput(input);
        }
    }

    public static void AddMixerInput(this MixingSampleProvider mixer, ISampleProvider input,
        float volume, float balance, out ISampleProvider rootSample)
    {
        var adjustVolume = volume >= 1 ? input : input.AddToAdjustVolume(volume);
        var adjustBalance = balance == 0 ? adjustVolume : adjustVolume.AddToBalanceProvider(balance);

        rootSample = adjustBalance;
        mixer.AddMixerInput(adjustBalance);
    }

    private static void PlayAudio(MixingSampleProvider mixer, CachedAudio cachedAudio, SampleControl? sampleControl,
        out ISampleProvider? rootSample)
    {
        mixer.AddMixerInput(new CachedAudioSampleProvider(cachedAudio), sampleControl, out rootSample);
    }

    private static void PlayAudio(MixingSampleProvider mixer, CachedAudio cachedAudio, float volume, float balance,
        out ISampleProvider? rootSample)
    {
        mixer.AddMixerInput(new CachedAudioSampleProvider(cachedAudio), volume, balance, out rootSample);
    }

    private static EnhancedVolumeSampleProvider AddToAdjustVolume(this ISampleProvider input, float volume)
    {
        var volumeSampleProvider = new EnhancedVolumeSampleProvider(input)
        {
            Volume = volume
        };
        return volumeSampleProvider;
    }

    private static BalanceSampleProvider AddToBalanceProvider(this ISampleProvider input, float balance)
    {
        var volumeSampleProvider = new BalanceSampleProvider(input)
        {
            Balance = balance
        };
        return volumeSampleProvider;
    }
}

[SupportedOSPlatform("windows6.0")]
public class AudioPlaybackEngine : IDisposable, INotifyPropertyChanged
{
    private readonly CachedAudioFactory _cachedAudioFactory;

    private EnhancedVolumeSampleProvider? _volumeProvider;

    public AudioPlaybackEngine(CachedAudioFactory cachedAudioFactory, IWavePlayer? outputDevice,
        WaveFormat? waveFormat = null)
    {
        _cachedAudioFactory = cachedAudioFactory;
        Context = SynchronizationContext.Current ?? new SingleSynchronizationContext("AudioPlaybackEngine_STA",
            staThread: true, threadPriority: ThreadPriority.AboveNormal);
        OutputDevice = outputDevice;
        Initialize(waveFormat ?? new WaveFormat(44100, 2));
    }

    public AudioPlaybackEngine(CachedAudioFactory cachedAudioFactory, DeviceCreationHelper deviceCreationHelper,
        DeviceDescription? deviceDescription, WaveFormat? waveFormat = null)
    {
        _cachedAudioFactory = cachedAudioFactory;
        Context = SynchronizationContext.Current ?? new SingleSynchronizationContext("AudioPlaybackEngine_STA",
            staThread: true, threadPriority: ThreadPriority.AboveNormal);
        var (player, _) = deviceCreationHelper.CreateDevice(deviceDescription, Context);
        OutputDevice = player;
        Initialize(waveFormat ?? new WaveFormat(44100, 2));
    }

    public IWavePlayer? OutputDevice { get; }
    public WaveFormat FileWaveFormat { get; private set; } = null!;
    public WaveFormat WaveFormat { get; private set; } = null!;
    public MixingSampleProvider RootMixer { get; private set; } = null!;
    public ISampleProvider RootSampleProvider { get; private set; } = null!;
    public SynchronizationContext Context { get; private set; } = null!;

    public float Volume
    {
        get => _volumeProvider?.Volume ?? 1;
        set
        {
            if (_volumeProvider == null) return;
            if (value.Equals(_volumeProvider.Volume)) return;
            _volumeProvider.Volume = value;
            OnPropertyChanged();
        }
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
        var rootSample = await RootMixer.PlayAudio(_cachedAudioFactory, path, sampleControl).ConfigureAwait(false);
        return rootSample;
    }

    public async Task<ISampleProvider?> PlayAudio(string path, float volume, float balance = 0)
    {
        var rootSample = await RootMixer.PlayAudio(_cachedAudioFactory, path, balance, volume).ConfigureAwait(false);
        return rootSample;
    }

    public void Dispose()
    {
        OutputDevice?.Dispose();

        if (Context is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private void Initialize(WaveFormat waveFormat)
    {
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(waveFormat.SampleRate, waveFormat.Channels);
        FileWaveFormat = waveFormat;
        RootMixer = new MixingSampleProvider(WaveFormat)
        {
            ReadFully = true
        };

        ISampleProvider root = RootMixer;
        root = _volumeProvider = new EnhancedVolumeSampleProvider(root);

        if (OutputDevice != null)
        {
            Exception? ex = null;
            Context.Send(_ =>
            {
                try
                {
                    OutputDevice.Init(root);
                }
                catch (Exception e)
                {
                    ex = e;
                }
            }, null);

            if (ex != null) throw ex;
            OutputDevice.Play();
        }

        RootSampleProvider = root;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}