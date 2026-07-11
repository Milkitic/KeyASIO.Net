using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeyAsio.Core.Audio;
using KeyAsio.Core.Audio.SampleProviders.BalancePans;
using KeyAsio.Lang;
using KeyAsio.Services;
using KeyAsio.Shared;
using Microsoft.Extensions.Logging;
using NAudio.Wave;
using SukiUI.Toasts;
using System.Collections.ObjectModel;

namespace KeyAsio.ViewModels;

public partial class AudioSettingsViewModel : ObservableObject
{
    public event Action<DeviceDescription?>? OnDeviceChanged;

    private readonly ILogger<AudioSettingsViewModel> _logger;
    private readonly IAudioDeviceManager _audioDeviceManager;
    private readonly IAudioDeviceOperationCoordinator _deviceOperations;
    private readonly AppSettings _appSettings;

    private bool _isInitializing;
    private (DeviceDescription? PlaybackDevice, int SampleRate) _originalAudioSettings;

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
            PlaybackEngine = null!;
            _deviceOperations = null!;
        }
    }

    public AudioSettingsViewModel(ILogger<AudioSettingsViewModel> logger,
        AppSettings appSettings,
        IAudioDeviceManager audioDeviceManager,
        IPlaybackEngine playbackEngine,
        IAudioDeviceOperationCoordinator deviceOperations)
    {
        _logger = logger;
        _appSettings = appSettings;
        _audioDeviceManager = audioDeviceManager;
        _deviceOperations = deviceOperations;
        PlaybackEngine = playbackEngine;

        PlaybackEngine.DeviceError += PlaybackEngine_DeviceError;

        _ = InitializeAudioSettingsAsync();
    }

    public int[] SupportedSampleRates { get; } = [44100, 48000, 96000, 192000];
    public WavePlayerType[] AvailableDriverTypes { get; } = Enum.GetValues<WavePlayerType>();
    public LimiterType[] AvailableLimiterTypes { get; } = Enum.GetValues<LimiterType>();
    public BalanceMode[] AvailableBalanceModes { get; } = Enum.GetValues<BalanceMode>();

    public IPlaybackEngine PlaybackEngine { get; }
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
    public partial string FramesPerBuffer { get; set; }

    [ObservableProperty]
    public partial double AsioLatencyMs { get; set; }

    [ObservableProperty]
    public partial string? DeviceErrorMessage { get; set; }

    [ObservableProperty]
    public partial string? DeviceFullErrorMessage { get; set; }

    [ObservableProperty]
    public partial string InfoBarTitle { get; set; } = SRKeys.Audio_InfoBar_Title_Ready;

    [ObservableProperty]
    public partial string InfoBarMessage { get; set; } = SRKeys.Audio_InfoBar_Message_AsioReady;

    [ObservableProperty]
    public partial NotificationType InfoBarSeverity { get; set; } = NotificationType.Success;

    private bool _hasAsio;

    private void UpdateInfoBarState()
    {
        if (SelectedDriverType == WavePlayerType.ASIO)
        {
            if (!_hasAsio)
            {
                InfoBarSeverity = NotificationType.Error;
                InfoBarTitle = SRKeys.Audio_InfoBar_Title_Error;
                InfoBarMessage = SRKeys.Audio_InfoBar_Message_AsioMissing;
            }
            else
            {
                InfoBarSeverity = NotificationType.Success;
                InfoBarTitle = SRKeys.Audio_InfoBar_Title_Ready;
                InfoBarMessage = SRKeys.Audio_InfoBar_Message_AsioReady;
            }
        }
        else if (SelectedDriverType == WavePlayerType.DirectSound)
        {
            InfoBarSeverity = NotificationType.Warning;
            InfoBarTitle = SRKeys.Audio_InfoBar_Title_Attention;
            InfoBarMessage = _hasAsio
                ? SRKeys.Audio_InfoBar_Message_DirectSound_AsioAvailable
                : SRKeys.Audio_InfoBar_Message_DirectSound_WasapiAvailable;
        }
        else if (SelectedDriverType == WavePlayerType.WASAPI)
        {
            if (_hasAsio)
            {
                InfoBarSeverity = NotificationType.Warning;
                InfoBarTitle = SRKeys.Audio_InfoBar_Title_Suggestion;
                InfoBarMessage = SRKeys.Audio_InfoBar_Message_AsioDetected;
            }
            else
            {
                if (!IsExclusiveMode)
                {
                    InfoBarSeverity = NotificationType.Warning;
                    InfoBarTitle = SRKeys.Audio_InfoBar_Title_Attention;
                    InfoBarMessage = SRKeys.Audio_InfoBar_Message_WasapiNonExclusive;
                }
                else
                {
                    InfoBarSeverity = NotificationType.Success;
                    InfoBarTitle = SRKeys.Audio_InfoBar_Title_Ready;
                    InfoBarMessage = SRKeys.Audio_InfoBar_Message_WasapiExclusiveReady;
                }
            }
        }
    }

    public async Task InitializeDevice()
    {
        if (_appSettings.Audio.PlaybackDevice == null) return;
        ApplyOperationResult(await _deviceOperations.InitializeConfiguredAsync());
    }

    [RelayCommand]
    public async Task ApplyAudioSettings()
    {
        DeviceErrorMessage = null;
        DeviceFullErrorMessage = null;
        DeviceDescription? requestedDevice = null;
        if (SelectedAudioDevice != null)
        {
            requestedDevice = SelectedAudioDevice with
            {
                Latency = (int)TargetBufferSize,
                IsExclusive = IsExclusiveMode,
                ForceASIOBufferSize = (ushort)ForceAsioBufferSize
            };
        }

        var result = await _deviceOperations.ApplyAsync(requestedDevice, SelectedSampleRate);
        ApplyOperationResult(result);
        if (!result.IsSuccess)
        {
            ShowOperationFailure("Device Initialization Failed", result);
            CheckAudioChanges();
            return;
        }

        _originalAudioSettings = (requestedDevice, SelectedSampleRate);
        CheckAudioChanges();
        ToastManager?.CreateSimpleInfoToast()
            .WithTitle("Audio Settings Applied")
            .WithContent(requestedDevice is null
                ? "Audio output disabled."
                : $"Successfully applied new device: {result.ActiveDevice?.FriendlyName}")
            .Queue();
    }

    [RelayCommand]
    public void DiscardAudioSettings()
    {
        _ = InitializeAudioSettingsAsync();
    }

    [RelayCommand]
    public void OpenAsioPanel()
    {
        if (PlaybackEngine.CurrentDevice is AsioOut asioOut)
        {
            asioOut.ShowControlPanel();
        }
    }

    [RelayCommand]
    public async Task ReloadAudioDevice()
    {
        if (_appSettings.Audio.PlaybackDevice == null) return;

        var result = await _deviceOperations.ReloadAsync();
        ApplyOperationResult(result);
        if (result.IsSuccess)
        {
            ToastManager?.CreateSimpleInfoToast()
                .WithTitle("Device Reloaded")
                .WithContent($"Successfully reloaded device: {result.ActiveDevice?.FriendlyName}")
                .Queue();
        }
        else
        {
            ShowOperationFailure("Device Reload Failed", result);
        }
    }

    [RelayCommand]
    public async Task ClearAudioDevice()
    {
        DeviceErrorMessage = null;
        DeviceFullErrorMessage = null;
        var result = await _deviceOperations.ClearAsync();
        ApplyOperationResult(result);
        if (!result.IsSuccess)
        {
            ShowOperationFailure("Failed to Disable Audio", result);
            return;
        }

        // Also update UI selection if we are on settings page
        SelectedAudioDevice = null;
        _originalAudioSettings = (null, _appSettings.Audio.SampleRate);
        CheckAudioChanges();
    }

    async partial void OnSelectedDriverTypeChanged(WavePlayerType value)
    {
        try
        {
            var devices = await _audioDeviceManager.GetCachedAvailableDevicesAsync();
            _hasAsio = devices.Any(d => d.WavePlayerType == WavePlayerType.ASIO);
            var filtered = devices.Where(d => d.WavePlayerType == value).ToList();
            AvailableAudioDevices = new ObservableCollection<DeviceDescription>(filtered);

            if (_isInitializing) return;
            // If the current device is not compatible with the new driver type, select the first available one
            if (SelectedAudioDevice?.WavePlayerType != value)
            {
                SelectedAudioDevice = filtered.FirstOrDefault();
            }

            CheckAudioChanges();
            UpdateInfoBarState();
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

    partial void OnTargetBufferSizeChanged(double value) => CheckAudioChanges();

    partial void OnForceAsioBufferSizeChanged(int value) => CheckAudioChanges();

    partial void OnIsExclusiveModeChanged(bool value)
    {
        CheckAudioChanges();
        UpdateInfoBarState();
    }

    private async Task InitializeAudioSettingsAsync()
    {
        _isInitializing = true;
        try
        {
            // Save original settings for dirty checking
            _originalAudioSettings = (_appSettings.Audio.PlaybackDevice, _appSettings.Audio.SampleRate);

            var devices = await _audioDeviceManager.GetCachedAvailableDevicesAsync();
            _hasAsio = devices.Any(d => d.WavePlayerType == WavePlayerType.ASIO);

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
        }
        finally
        {
            _isInitializing = false;
            // Force check initial state (should be false)
            CheckAudioChanges();
            UpdateInfoBarState();
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
            !DeviceComparer.AreSettingsEqual(potentialDevice, _originalAudioSettings.PlaybackDevice) ||
            SelectedSampleRate != _originalAudioSettings.SampleRate;
    }

    private void ApplyOperationResult(AudioDeviceOperationResult result)
    {
        if (result.IsSuccess)
        {
            DeviceErrorMessage = null;
            DeviceFullErrorMessage = null;
        }
        else
        {
            DeviceErrorMessage = result.RollbackError is null
                ? result.Error?.Message
                : $"{result.Error?.Message} (rollback also failed: {result.RollbackError.Message})";
            DeviceFullErrorMessage = result.RollbackError is null
                ? result.Error?.ToString()
                : $"{result.Error}\n\nRollback failure:\n{result.RollbackError}";
        }

        if (PlaybackEngine.CurrentDevice is AsioOut asioOut &&
            PlaybackEngine.CurrentDeviceDescription is { } actualDevice)
        {
            FramesPerBuffer = $"{asioOut.FramesPerBuffer}→{actualDevice.AsioActualSamples} samples";
            AsioLatencyMs = actualDevice.AsioLatencyMs;
        }

        OnDeviceChanged?.Invoke(result.ActiveDevice);
    }

    private void ShowOperationFailure(string title, AudioDeviceOperationResult result) =>
        ToastManager?.CreateToast()
            .WithTitle(title)
            .WithContent(DeviceErrorMessage ?? "Unknown audio device error.")
            .OfType(NotificationType.Error)
            .Dismiss().After(TimeSpan.FromSeconds(result.RollbackError is null ? 5 : 10))
            .Dismiss().ByClicking()
            .Queue();

    private void PlaybackEngine_DeviceError(Exception ex)
    {
        _ = Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            DeviceErrorMessage = ex.Message;
            DeviceFullErrorMessage = ex.ToString();
            ToastManager?.CreateToast()
                .WithTitle("Device Error")
                .WithContent(ex.Message)
                .OfType(NotificationType.Error)
                .Dismiss().After(TimeSpan.FromSeconds(5))
                .Dismiss().ByClicking()
                .Queue();
        });
    }
}
