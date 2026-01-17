using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeyAsio.Core.Audio;
using KeyAsio.Shared;
using KeyAsio.Shared.Sync.Services;
using Microsoft.Extensions.Logging;
using Milki.Extensions.Configuration;
using NAudio.Wave;
using SukiUI.Toasts;

namespace KeyAsio.ViewModels;

public partial class AudioSettingsViewModel : ObservableObject
{
    public event Action<DeviceDescription?>? OnDeviceChanged;

    private readonly ILogger<AudioSettingsViewModel> _logger;
    private readonly AudioDeviceManager _audioDeviceManager;
    private readonly AppSettings _appSettings;
    private readonly GameplayAudioService _gameplayAudioService;

    private bool _isInitializing;
    private (DeviceDescription? PlaybackDevice, int SampleRate, bool EnableLimiter) _originalAudioSettings;

    public AudioSettingsViewModel()
    {
        if (!Design.IsDesignMode)
        {
            throw new NotSupportedException();
        }
        else
        {
            _appSettings = new AppSettings();
            _audioDeviceManager = null!;
            _logger = null!;
            AudioEngine = null!;
            _gameplayAudioService = null!;
        }
    }

    public AudioSettingsViewModel(ILogger<AudioSettingsViewModel> logger,
        AppSettings appSettings,
        AudioDeviceManager audioDeviceManager,
        AudioEngine audioEngine,
        GameplayAudioService gameplayAudioService)
    {
        _logger = logger;
        _appSettings = appSettings;
        _audioDeviceManager = audioDeviceManager;
        _gameplayAudioService = gameplayAudioService;
        AudioEngine = audioEngine;

        _ = InitializeAudioSettingsAsync();
    }

    public int[] SupportedSampleRates { get; } = [44100, 48000, 96000, 192000];
    public WavePlayerType[] AvailableDriverTypes { get; } = Enum.GetValues<WavePlayerType>();

    public AudioEngine AudioEngine { get; }
    public ISukiToastManager? ToastManager { get; set; }

    [ObservableProperty]
    public partial bool HasUnsavedAudioChanges { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<DeviceDescription> AvailableAudioDevices { get; set; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAsio))]
    [NotifyPropertyChangedFor(nameof(IsWasapi))]
    [NotifyPropertyChangedFor(nameof(IsDirectSound))]
    public partial DeviceDescription? SelectedAudioDevice { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAsio))]
    [NotifyPropertyChangedFor(nameof(IsWasapi))]
    [NotifyPropertyChangedFor(nameof(IsDirectSound))]
    public partial WavePlayerType SelectedDriverType { get; set; }
    public bool IsAsio => SelectedDriverType == WavePlayerType.ASIO && SelectedAudioDevice != null;
    public bool IsWasapi => SelectedDriverType == WavePlayerType.WASAPI && SelectedAudioDevice != null;
    public bool IsDirectSound => SelectedDriverType == WavePlayerType.DirectSound && SelectedAudioDevice != null;

    [ObservableProperty]
    public partial double TargetBufferSize { get; set; }

    [ObservableProperty]
    public partial int ForceAsioBufferSize { get; set; }

    [ObservableProperty]
    public partial bool IsExclusiveMode { get; set; }

    [ObservableProperty]
    public partial int SelectedSampleRate { get; set; }

    [ObservableProperty]
    public partial bool IsLimiterEnabled { get; set; }

    [ObservableProperty]
    public partial string FramesPerBuffer { get; set; }

    [ObservableProperty]
    public partial double AsioLatencyMs { get; set; }

    [ObservableProperty]
    public partial string? DeviceErrorMessage { get; set; }

    [ObservableProperty]
    public partial string? DeviceFullErrorMessage { get; set; }

    public async Task InitializeDevice()
    {
        if (_appSettings.Audio.PlaybackDevice == null) return;
        await LoadDevice(_appSettings.Audio.PlaybackDevice);
    }

    [RelayCommand]
    public async Task ApplyAudioSettings()
    {
        DeviceErrorMessage = null;
        DeviceFullErrorMessage = null;
        try
        {
            if (SelectedAudioDevice != null)
            {
                _appSettings.Audio.PlaybackDevice = SelectedAudioDevice with
                {
                    Latency = (int)TargetBufferSize,
                    IsExclusive = IsExclusiveMode,
                    ForceASIOBufferSize = (ushort)ForceAsioBufferSize
                };
            }
            else
            {
                _appSettings.Audio.PlaybackDevice = null;
            }

            _appSettings.Audio.SampleRate = SelectedSampleRate;
            _appSettings.Audio.EnableLimiter = IsLimiterEnabled;

            _originalAudioSettings = (_appSettings.Audio.PlaybackDevice, _appSettings.Audio.SampleRate,
                _appSettings.Audio.EnableLimiter);
            _appSettings.Save();
            CheckAudioChanges();

            if (SelectedAudioDevice != null)
            {
                await DisposeDeviceAsync();
                await InitializeDevice();

                if (DeviceErrorMessage != null)
                {
                    ToastManager?.CreateToast()
                        .WithTitle("Device Initialization Failed")
                        .WithContent(DeviceErrorMessage)
                        .OfType(NotificationType.Error)
                        .Dismiss().After(TimeSpan.FromSeconds(5))
                        .Dismiss().ByClicking()
                        .Queue();
                }
                else
                {
                    ToastManager?.CreateSimpleInfoToast()
                        .WithTitle("Audio Settings Applied")
                        .WithContent($"Successfully applied new device: {AudioEngine.CurrentDeviceDescription?.FriendlyName}")
                        .Queue();
                }
            }
            else
            {
                await DisposeDeviceAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurs while applying audio settings.");
        }
    }

    [RelayCommand]
    public void DiscardAudioSettings()
    {
        _ = InitializeAudioSettingsAsync();
    }

    [RelayCommand]
    public void OpenAsioPanel()
    {
        if (AudioEngine.CurrentDevice is AsioOut asioOut)
        {
            asioOut.ShowControlPanel();
        }
    }

    [RelayCommand]
    public async Task ReloadAudioDevice()
    {
        if (_appSettings.Audio.PlaybackDevice != null)
        {
            await DisposeDeviceAsync();
            await InitializeDevice();

            if (AudioEngine.CurrentDeviceDescription != null)
            {
                ToastManager?.CreateSimpleInfoToast()
                    .WithTitle("Device Reloaded")
                    .WithContent($"Successfully reloaded device: {AudioEngine.CurrentDeviceDescription.FriendlyName}")
                    .Queue();
            }
            else if (DeviceErrorMessage != null)
            {
                ToastManager?.CreateToast()
                    .WithTitle("Device Reload Failed")
                    .WithContent(DeviceErrorMessage)
                    .OfType(NotificationType.Error)
                    .Dismiss().After(TimeSpan.FromSeconds(8))
                    .Dismiss().ByClicking()
                    .Queue();
            }
        }
    }

    [RelayCommand]
    public async Task ClearAudioDevice()
    {
        DeviceErrorMessage = null;
        DeviceFullErrorMessage = null;
        _appSettings.Audio.PlaybackDevice = null;
        _appSettings.Save();
        await DisposeDeviceAsync();

        // Also update UI selection if we are on settings page
        SelectedAudioDevice = null;
        _originalAudioSettings = (null, _appSettings.Audio.SampleRate, _appSettings.Audio.EnableLimiter);
        CheckAudioChanges();
    }

    async partial void OnSelectedDriverTypeChanged(WavePlayerType value)
    {
        try
        {
            var devices = await _audioDeviceManager.GetCachedAvailableDevicesAsync();
            var filtered = devices.Where(d => d.WavePlayerType == value).ToList();
            AvailableAudioDevices = new ObservableCollection<DeviceDescription>(filtered);

            if (_isInitializing) return;
            // If the current device is not compatible with the new driver type, select the first available one
            if (SelectedAudioDevice?.WavePlayerType != value)
            {
                SelectedAudioDevice = filtered.FirstOrDefault();
            }

            CheckAudioChanges();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to update audio devices for driver type {DriverType}", value);
        }
    }

    partial void OnSelectedAudioDeviceChanged(DeviceDescription? value)
    {
        if (value != null && !_isInitializing)
        {
            TargetBufferSize = value.Latency;
            IsExclusiveMode = value.IsExclusive;
            ForceAsioBufferSize = value.ForceASIOBufferSize;
        }

        CheckAudioChanges();
    }

    partial void OnSelectedSampleRateChanged(int value) => CheckAudioChanges();

    partial void OnIsLimiterEnabledChanged(bool value) => CheckAudioChanges();

    partial void OnTargetBufferSizeChanged(double value) => CheckAudioChanges();

    partial void OnForceAsioBufferSizeChanged(int value) => CheckAudioChanges();

    partial void OnIsExclusiveModeChanged(bool value) => CheckAudioChanges();

    private async Task InitializeAudioSettingsAsync()
    {
        _isInitializing = true;
        try
        {
            // Save original settings for dirty checking
            _originalAudioSettings = (_appSettings.Audio.PlaybackDevice, _appSettings.Audio.SampleRate,
                _appSettings.Audio.EnableLimiter);

            var devices = await _audioDeviceManager.GetCachedAvailableDevicesAsync();

            if (_appSettings.Audio.PlaybackDevice != null)
            {
                SelectedDriverType = _appSettings.Audio.PlaybackDevice.WavePlayerType;

                var filtered = devices.Where(d => d.WavePlayerType == SelectedDriverType).ToList();
                AvailableAudioDevices = new ObservableCollection<DeviceDescription>(filtered);

                var match = filtered.FirstOrDefault(d => d.DeviceId == _appSettings.Audio.PlaybackDevice.DeviceId);
                SelectedAudioDevice = match ?? filtered.FirstOrDefault();

                TargetBufferSize = _appSettings.Audio.PlaybackDevice.Latency;
                IsExclusiveMode = _appSettings.Audio.PlaybackDevice.IsExclusive;
                ForceAsioBufferSize = _appSettings.Audio.PlaybackDevice.ForceASIOBufferSize;
            }
            else
            {
                SelectedDriverType = WavePlayerType.WASAPI;
                // Trigger logic manually if needed, but OnSelectedDriverTypeChanged might be called by property setter if not careful.
                // Since _isInitializing is true, the logic in OnSelectedDriverTypeChanged mostly skips side effects, except filling list.
                // We need to fill the list.
                var filtered = devices.Where(d => d.WavePlayerType == SelectedDriverType).ToList();
                AvailableAudioDevices = new ObservableCollection<DeviceDescription>(filtered);
                SelectedAudioDevice = null;
            }

            SelectedSampleRate = _appSettings.Audio.SampleRate;
            IsLimiterEnabled = _appSettings.Audio.EnableLimiter;
        }
        finally
        {
            _isInitializing = false;
            // Force check initial state (should be false)
            CheckAudioChanges();
        }
    }

    private void CheckAudioChanges()
    {
        if (_isInitializing) return;

        // Construct potential new device description to compare
        DeviceDescription? potentialDevice = null;
        if (SelectedAudioDevice != null)
        {
            potentialDevice = SelectedAudioDevice with
            {
                Latency = (int)TargetBufferSize,
                IsExclusive = IsExclusiveMode,
                ForceASIOBufferSize = (ushort)ForceAsioBufferSize
            };
        }

        HasUnsavedAudioChanges =
            !AreDevicesEqual(potentialDevice, _originalAudioSettings.PlaybackDevice) ||
            SelectedSampleRate != _originalAudioSettings.SampleRate ||
            IsLimiterEnabled != _originalAudioSettings.EnableLimiter;
    }

    private async Task LoadDevice(DeviceDescription deviceDescription)
    {
        DeviceErrorMessage = null;
        DeviceFullErrorMessage = null;
        try
        {
            AudioEngine.EnableLimiter = _appSettings.Audio.EnableLimiter;
            AudioEngine.MainVolume = _appSettings.Audio.MasterVolume / 100f;
            AudioEngine.MusicVolume = _appSettings.Audio.MusicVolume / 100f;
            AudioEngine.EffectVolume = _appSettings.Audio.EffectVolume / 100f;
            AudioEngine.StartDevice(deviceDescription, new WaveFormat(SelectedSampleRate, 2));

            if (AudioEngine.CurrentDevice is AsioOut asioOut)
            {
                var actualDd = AudioEngine.CurrentDeviceDescription;
                asioOut.DriverResetRequest += AsioOut_DriverResetRequest;
                FramesPerBuffer = $"{asioOut.FramesPerBuffer}→{actualDd.AsioActualSamples} samples";
                AsioLatencyMs = actualDd.AsioLatencyMs;
            }

            OnDeviceChanged?.Invoke(AudioEngine.CurrentDeviceDescription);
        }
        catch (Exception ex)
        {
            DeviceErrorMessage = ex.Message;
            DeviceFullErrorMessage = ex.ToString();
            _logger.LogError(ex, "Error occurs while creating device.");
            await DisposeDeviceAsync();
        }
    }

    private async void AsioOut_DriverResetRequest(object? sender, EventArgs e)
    {
        try
        {
            var deviceDescription = _appSettings.Audio.PlaybackDevice;
            if (deviceDescription == null) return;

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await DisposeDeviceAsync();
                await LoadDevice(deviceDescription);

                if (DeviceErrorMessage != null)
                {
                    ToastManager?.CreateToast()
                        .WithTitle("Device Reset Failed")
                        .WithContent(DeviceErrorMessage)
                        .OfType(NotificationType.Error)
                        .Dismiss().After(TimeSpan.FromSeconds(3))
                        .Dismiss().ByClicking()
                        .Queue();
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while resetting ASIO driver.");
        }
    }

    private async ValueTask DisposeDeviceAsync()
    {
        OnDeviceChanged?.Invoke(null);
        if (AudioEngine.CurrentDevice is AsioOut asioOut)
        {
            asioOut.DriverResetRequest -= AsioOut_DriverResetRequest;
        }

        for (int i = 0; i < 3; i++)
        {
            try
            {
                AudioEngine.CurrentDevice?.Dispose();
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while disposing device.");
                await Task.Delay(100);
            }
        }

        AudioEngine.StopDevice();
        _gameplayAudioService.ClearCaches();
    }

    private static bool AreDevicesEqual(DeviceDescription? d1, DeviceDescription? d2)
    {
        if (d1 == null && d2 == null) return true;
        if (d1 == null || d2 == null) return false;
        if (d1.WavePlayerType == WavePlayerType.ASIO)
        {
            return d1.WavePlayerType == d2.WavePlayerType &&
                   d1.DeviceId == d2.DeviceId &&
                   d1.ForceASIOBufferSize == d2.ForceASIOBufferSize;
        }

        if (d1.WavePlayerType == WavePlayerType.WASAPI)
        {
            return d1.WavePlayerType == d2.WavePlayerType &&
                   d1.DeviceId == d2.DeviceId &&
                   d1.Latency == d2.Latency &&
                   d1.IsExclusive == d2.IsExclusive;
        }

        return d1.WavePlayerType == d2.WavePlayerType &&
               d1.DeviceId == d2.DeviceId &&
               d1.Latency == d2.Latency;
    }
}