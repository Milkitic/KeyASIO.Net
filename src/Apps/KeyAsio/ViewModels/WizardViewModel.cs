using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeyAsio.Core.Audio;
using KeyAsio.Lang;
using KeyAsio.Services;
using KeyAsio.Shared;

namespace KeyAsio.ViewModels;

public enum WizardMode
{
    NotSelected,
    Hardware,
    Software
}

public partial class WizardViewModel : ViewModelBase
{
    private readonly SettingsManager _settingsManager;
    private readonly AudioDeviceManager _audioDeviceManager;
    private readonly AudioEngine _audioEngine;
    private readonly AppSettings _appSettings;

    [ObservableProperty]
    private int _stepIndex;

    [ObservableProperty]
    private ObservableCollection<string> _steps;

    [ObservableProperty]
    private WizardMode _selectedMode = WizardMode.NotSelected;

    // Config Page
    [ObservableProperty]
    private ObservableCollection<WavePlayerType> _availableDriverTypes = new();

    [ObservableProperty]
    private WavePlayerType _selectedDriverType;

    [ObservableProperty]
    private ObservableCollection<DeviceDescription> _availableAudioDevices = new();

    [ObservableProperty]
    private DeviceDescription? _selectedAudioDevice;

    // ProMix specific
    [ObservableProperty]
    private bool _isVirtualDriverDetected;

    // Privacy
    [ObservableProperty]
    private bool _enableCrashReport = true;

    [ObservableProperty]
    private bool _enableUpdates = true;

    public WizardViewModel()
    {
        if (!Design.IsDesignMode)
            throw new InvalidOperationException("This constructor is only for design-time purposes.");
        Steps =
        [
            SR.Wizard_Title, // Welcome
            SR.Wizard_Mode_Title, // Mode Selection
            SR.Wizard_Config_Title, // Configuration
            SR.Wizard_Test_Title, // Test
            SR.Wizard_Privacy_Title
        ];
    }

    public WizardViewModel(
        SettingsManager settingsManager,
        AudioDeviceManager audioDeviceManager,
        AudioEngine audioEngine,
        AppSettings appSettings)
    {
        _settingsManager = settingsManager;
        _audioDeviceManager = audioDeviceManager;
        _audioEngine = audioEngine;
        _appSettings = appSettings;

        Steps =
        [
            SR.Wizard_Title, // Welcome
            SR.Wizard_Mode_Title, // Mode Selection
            SR.Wizard_Config_Title, // Configuration
            SR.Wizard_Test_Title, // Test
            SR.Wizard_Privacy_Title
        ];

        AvailableDriverTypes = new ObservableCollection<WavePlayerType>(Enum.GetValues<WavePlayerType>());
        SelectedDriverType = WavePlayerType.ASIO;

        LoadDevices();
    }

    [RelayCommand]
    private async Task Next()
    {
        if (StepIndex < Steps.Count - 1)
        {
            // Validation before moving
            if (StepIndex == 1 && SelectedMode == WizardMode.NotSelected)
            {
                // Show toast? For now just return
                return;
            }

            if (StepIndex == 2) // Moving from Config to Test
            {
                try
                {
                    if (SelectedAudioDevice != null)
                    {
                        _audioEngine.StopDevice();
                        _audioEngine.StartDevice(SelectedAudioDevice);
                    }
                }
                catch (Exception ex)
                {
                    // TODO: Show error dialog
                    return;
                }
            }

            StepIndex++;

            if (StepIndex == 2) // Config page
            {
                if (SelectedMode == WizardMode.Software)
                {
                    CheckVirtualDriver();
                }
            }
        }
        else
        {
            Finish();
        }
    }

    [RelayCommand]
    private void Previous()
    {
        if (StepIndex > 0)
        {
            StepIndex--;
        }
    }

    [RelayCommand]
    private void SelectMode(WizardMode mode)
    {
        SelectedMode = mode;
        Next();
    }

    [RelayCommand]
    private void Skip()
    {
        Finish();
    }

    private void Finish()
    {
        // Save settings
        _appSettings.Logging.EnableErrorReporting = EnableCrashReport;
        // _appSettings.Update.EnableAutoUpdate = EnableUpdates; // Assuming this exists or will be added

        _appSettings.General.IsFirstRun = false;
        // Trigger close window
        OnRequestClose?.Invoke();
    }

    public event Action? OnRequestClose;

    private async void LoadDevices()
    {
        var devices = await _audioDeviceManager.GetCachedAvailableDevicesAsync();
        UpdateDeviceList(devices);
    }

    partial void OnSelectedDriverTypeChanged(WavePlayerType value)
    {
        LoadDevices();
    }

    private async void UpdateDeviceList(IReadOnlyList<DeviceDescription> allDevices)
    {
        var filtered = allDevices.Where(d => d.WavePlayerType == SelectedDriverType).ToList();
        AvailableAudioDevices = new ObservableCollection<DeviceDescription>(filtered);
        if (AvailableAudioDevices.Any())
        {
            SelectedAudioDevice = AvailableAudioDevices.First();
        }
    }

    private void CheckVirtualDriver()
    {
        // Simple check for VB-Cable or Voicemeeter
        // This is a simplified check.
        Dispatcher.UIThread.Post(async () =>
        {
            var devices = await _audioDeviceManager.GetCachedAvailableDevicesAsync();
            var wasapiDevices = devices.Where(d => d.WavePlayerType == WavePlayerType.WASAPI).ToList();
            IsVirtualDriverDetected = wasapiDevices.Any(d =>
                d.FriendlyName?.Contains("CABLE", StringComparison.OrdinalIgnoreCase) == true ||
                d.FriendlyName?.Contains("VoiceMeeter", StringComparison.OrdinalIgnoreCase) == true);
        });
    }

    [RelayCommand]
    private async Task TestKeySound()
    {
        var path = _appSettings.Paths.HitsoundPath ?? "./resources/default/normal-hitnormal.ogg";
        try
        {
            if (File.Exists(path))
            {
                await _audioEngine.PlayAudio(path, 1.0f);
            }
        }
        catch
        {
            // ignored
        }
    }
}