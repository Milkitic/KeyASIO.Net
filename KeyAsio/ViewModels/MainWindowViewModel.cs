using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeyAsio.Audio;
using KeyAsio.Services;
using KeyAsio.Shared;
using KeyAsio.Shared.Models;
using Microsoft.Extensions.Logging;
using Milki.Extensions.Configuration;

namespace KeyAsio.ViewModels;

[ObservableObject]
public partial class MainWindowViewModel
{
    private readonly ILogger<MainWindowViewModel> _logger;
    private readonly AudioDeviceManager _audioDeviceManager;

    private bool _isInitializing;
    private (DeviceDescription? PlaybackDevice, int SampleRate, bool EnableLimiter) _originalAudioSettings;

    public MainWindowViewModel()
    {
        if (!Design.IsDesignMode)
        {
            throw new NotSupportedException();
        }
        else
        {
            AppSettings = new AppSettings();
        }
    }

    public MainWindowViewModel(ILogger<MainWindowViewModel> logger,
        AppSettings appSettings,
        UpdateService updateService,
        AudioDeviceManager audioDeviceManager)
    {
        AppSettings = appSettings;
        UpdateService = updateService;
        _logger = logger;
        _audioDeviceManager = audioDeviceManager;

        _ = InitializeAudioSettingsAsync();
    }

    public AppSettings AppSettings { get; }
    public UpdateService UpdateService { get; }

    public SliderTailPlaybackBehavior[] SliderTailBehaviors { get; } = Enum.GetValues<SliderTailPlaybackBehavior>();
    public int[] SupportedSampleRates { get; } = [44100, 48000, 96000, 192000];
    public WavePlayerType[] AvailableDriverTypes { get; } = Enum.GetValues<WavePlayerType>();

    [ObservableProperty]
    public partial bool HasUnsavedAudioChanges { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<DeviceDescription> AvailableAudioDevices { get; set; } = new();

    [ObservableProperty]
    public partial DeviceDescription? SelectedAudioDevice { get; set; }

    [ObservableProperty]
    public partial WavePlayerType SelectedDriverType { get; set; }

    [ObservableProperty]
    public partial double TargetBufferSize { get; set; } = 10;

    [ObservableProperty]
    public partial bool IsExclusiveMode { get; set; } = true;

    [ObservableProperty]
    public partial int SelectedSampleRate { get; set; }

    [ObservableProperty]
    public partial bool IsLimiterEnabled { get; set; }

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
        }

        CheckAudioChanges();
    }

    partial void OnSelectedSampleRateChanged(int value) => CheckAudioChanges();

    partial void OnIsLimiterEnabledChanged(bool value) => CheckAudioChanges();

    partial void OnTargetBufferSizeChanged(double value) => CheckAudioChanges();

    partial void OnIsExclusiveModeChanged(bool value) => CheckAudioChanges();

    private async Task InitializeAudioSettingsAsync()
    {
        _isInitializing = true;
        try
        {
            // Save original settings for dirty checking
            _originalAudioSettings = (AppSettings.Audio.PlaybackDevice, AppSettings.Audio.SampleRate,
                AppSettings.Audio.EnableLimiter);

            var devices = await _audioDeviceManager.GetCachedAvailableDevicesAsync();

            if (AppSettings.Audio.PlaybackDevice != null)
            {
                SelectedDriverType = AppSettings.Audio.PlaybackDevice.WavePlayerType;

                var filtered = devices.Where(d => d.WavePlayerType == SelectedDriverType).ToList();
                AvailableAudioDevices = new ObservableCollection<DeviceDescription>(filtered);

                var match = filtered.FirstOrDefault(d => d.DeviceId == AppSettings.Audio.PlaybackDevice.DeviceId);
                SelectedAudioDevice = match ?? filtered.FirstOrDefault();

                TargetBufferSize = AppSettings.Audio.PlaybackDevice.Latency;
                IsExclusiveMode = AppSettings.Audio.PlaybackDevice.IsExclusive;
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

            SelectedSampleRate = AppSettings.Audio.SampleRate;
            IsLimiterEnabled = AppSettings.Audio.EnableLimiter;
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
                IsExclusive = IsExclusiveMode
            };
        }

        HasUnsavedAudioChanges =
            !AreDevicesEqual(potentialDevice, _originalAudioSettings.PlaybackDevice) ||
            SelectedSampleRate != _originalAudioSettings.SampleRate ||
            IsLimiterEnabled != _originalAudioSettings.EnableLimiter;
    }

    [RelayCommand]
    private void ApplyAudioSettings()
    {
        if (SelectedAudioDevice != null)
        {
            AppSettings.Audio.PlaybackDevice = SelectedAudioDevice with
            {
                Latency = (int)TargetBufferSize,
                IsExclusive = IsExclusiveMode
            };
        }
        else
        {
            AppSettings.Audio.PlaybackDevice = null;
        }

        AppSettings.Audio.SampleRate = SelectedSampleRate;
        AppSettings.Audio.EnableLimiter = IsLimiterEnabled;

        _originalAudioSettings = (AppSettings.Audio.PlaybackDevice, AppSettings.Audio.SampleRate,
            AppSettings.Audio.EnableLimiter);
        AppSettings.Save();
        CheckAudioChanges();
    }

    [RelayCommand]
    private void DiscardAudioSettings()
    {
        _ = InitializeAudioSettingsAsync();
    }

    private static bool AreDevicesEqual(DeviceDescription? d1, DeviceDescription? d2)
    {
        if (d1 == null && d2 == null) return true;
        if (d1 == null || d2 == null) return false;

        return d1.WavePlayerType == d2.WavePlayerType &&
               d1.DeviceId == d2.DeviceId &&
               d1.Latency == d2.Latency &&
               d1.ForceASIOBufferSize == d2.ForceASIOBufferSize &&
               d1.IsExclusive == d2.IsExclusive;
    }
}