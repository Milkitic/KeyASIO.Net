using System.Collections.ObjectModel;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeyAsio.Audio;
using KeyAsio.Services;
using KeyAsio.Shared;
using KeyAsio.Shared.Models;
using Microsoft.Extensions.Logging;

namespace KeyAsio.ViewModels;

[ObservableObject]
public partial class MainWindowViewModel
{
    private readonly ILogger<MainWindowViewModel> _logger;
    private readonly AudioDeviceManager _audioDeviceManager;

    private bool _isInitializing;

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
    public partial ObservableCollection<DeviceDescription> AvailableAudioDevices { get; set; } = new();

    [ObservableProperty]
    public partial DeviceDescription? SelectedAudioDevice { get; set; }

    [ObservableProperty]
    public partial WavePlayerType SelectedDriverType { get; set; }

    [ObservableProperty]
    public partial double TargetBufferSize { get; set; } = 10;

    [ObservableProperty]
    public partial bool IsExclusiveMode { get; set; } = true;

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
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to update audio devices for driver type {DriverType}", value);
        }
    }

    private async Task InitializeAudioSettingsAsync()
    {
        _isInitializing = true;
        try
        {
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
                //OnSelectedDriverTypeChanged(SelectedDriverType);
            }
        }
        finally
        {
            _isInitializing = false;
        }
    }

    //partial void OnSelectedAudioDeviceChanged(DeviceDescription? value)
    //{
    //    if (value == null || _isInitializing) return;

    //    TargetBufferSize = value.Latency;
    //    IsExclusiveMode = value.IsExclusive;
    //    UpdatePlaybackDeviceSettings();
    //}

    //partial void OnTargetBufferSizeChanged(double value) => UpdatePlaybackDeviceSettings();

    //partial void OnIsExclusiveModeChanged(bool value) => UpdatePlaybackDeviceSettings();

    //private void UpdatePlaybackDeviceSettings()
    //{
    //    if (SelectedAudioDevice != null)
    //    {
    //        AppSettings.Audio.PlaybackDevice = SelectedAudioDevice with
    //        {
    //            Latency = (int)TargetBufferSize,
    //            IsExclusive = IsExclusiveMode
    //        };
    //    }
    //}

    [RelayCommand]
    private void ApplyAudioSettings()
    {
        // TODO: Apply settings to the audio engine
    }

    [RelayCommand]
    private void DiscardAudioSettings()
    {
        // TODO: Discard changes and revert to current settings
    }
}