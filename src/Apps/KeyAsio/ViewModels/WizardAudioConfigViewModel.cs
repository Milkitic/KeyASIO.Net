using System.Collections.ObjectModel;
using Avalonia.Controls.Notifications;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeyAsio.Core.Audio;
using SukiUI.Toasts;

namespace KeyAsio.ViewModels;

public enum WizardMode
{
    NotSelected,
    Hardware,
    Software
}

public enum AudioSubStep
{
    Selection,
    Configuration,
    Validation
}

public partial class WizardAudioConfigViewModel : ViewModelBase
{
    private readonly IAudioDeviceManager _audioDeviceManager;
    private readonly IPlaybackEngine _playbackEngine;
    private readonly ISukiToastManager _toastManager;

    public WizardAudioConfigViewModel(
        IAudioDeviceManager audioDeviceManager,
        IPlaybackEngine playbackEngine,
        ISukiToastManager toastManager)
    {
        _audioDeviceManager = audioDeviceManager;
        _playbackEngine = playbackEngine;
        _toastManager = toastManager;

        AvailableDriverTypes = new ObservableCollection<WavePlayerType>(Enum.GetValues<WavePlayerType>());
        SelectedDriverType = WavePlayerType.ASIO;

        LoadDevices();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsHardwareConfig))]
    [NotifyPropertyChangedFor(nameof(IsSoftwareConfig))]
    public partial WizardMode SelectedMode { get; set; } = WizardMode.NotSelected;

    // Config Page
    [ObservableProperty]
    public partial ObservableCollection<WavePlayerType> AvailableDriverTypes { get; set; }

    [ObservableProperty]
    public partial WavePlayerType SelectedDriverType { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<DeviceDescription> AvailableAudioDevices { get; set; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoForward))]
    public partial DeviceDescription? SelectedAudioDevice { get; set; }

    // ProMix specific
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoForward))]
    public partial bool IsVirtualDriverDetected { get; set; }

    // Audio Config Sub-stepper
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSelectionMode))]
    [NotifyPropertyChangedFor(nameof(IsHardwareConfig))]
    [NotifyPropertyChangedFor(nameof(IsSoftwareConfig))]
    [NotifyPropertyChangedFor(nameof(IsValidationStep))]
    [NotifyPropertyChangedFor(nameof(CanGoForward))]
    public partial AudioSubStep CurrentAudioSubStep { get; set; } = AudioSubStep.Selection;

    public bool IsSelectionMode => CurrentAudioSubStep == AudioSubStep.Selection;

    public bool IsHardwareConfig =>
        CurrentAudioSubStep == AudioSubStep.Configuration && SelectedMode == WizardMode.Hardware;

    public bool IsSoftwareConfig =>
        CurrentAudioSubStep == AudioSubStep.Configuration && SelectedMode == WizardMode.Software;

    public bool IsValidationStep => CurrentAudioSubStep == AudioSubStep.Validation;

    [ObservableProperty]
    public partial bool IsAudioConfigFinished { get; set; }

    [ObservableProperty]
    public partial string HardwareDriverWarning { get; set; } = "";

    [ObservableProperty]
    public partial bool ShowHardwareDriverWarning { get; set; }

    [ObservableProperty]
    public partial bool IsValidationRunning { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoForward))]
    public partial bool ValidationSuccess { get; set; }

    [ObservableProperty]
    public partial string ValidationMessage { get; set; } = "";


    public bool TryGoBack()
    {
        if (CurrentAudioSubStep == AudioSubStep.Configuration)
        {
            BackToSelection();
            return true;
        }

        if (CurrentAudioSubStep == AudioSubStep.Validation)
        {
            CurrentAudioSubStep = AudioSubStep.Configuration;
            IsValidationRunning = false;
            ValidationSuccess = false;
            IsAudioConfigFinished = false;
            _playbackEngine.StopDevice();
            return true;
        }

        return false;
    }

    public bool TryGoForward()
    {
        if (CurrentAudioSubStep == AudioSubStep.Configuration)
        {
            ApplyAndTestConfig();
            return true;
        }

        if (CurrentAudioSubStep == AudioSubStep.Validation)
        {
            if (ValidationSuccess)
            {
                // Allow proceeding to next main step
                return false;
            }
            else
            {
                // Retry
                ApplyAndTestConfig();
                return true;
            }
        }

        return false;
    }

    public bool CanGoForward
    {
        get
        {
            if (IsSelectionMode) return false;
            if (IsHardwareConfig) return SelectedAudioDevice != null;
            if (IsSoftwareConfig) return SelectedAudioDevice != null && IsVirtualDriverDetected;
            if (IsValidationStep) return true; // Can always retry or proceed if success
            return false;
        }
    }

    [RelayCommand]
    private void SelectMode(WizardMode mode)
    {
        SelectedMode = mode;
        if (mode == WizardMode.Hardware)
        {
            Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var devices = await _audioDeviceManager.GetCachedAvailableDevicesAsync();
                var asioCount = devices.Count(d => d.WavePlayerType == WavePlayerType.ASIO);
                if (asioCount < 1)
                {
                    ShowHardwareDriverWarning = true;
                    HardwareDriverWarning = "未检测到支持的驱动，建议切换到软件模式";
                }
                else
                {
                    ShowHardwareDriverWarning = false;
                }
            });
            SelectedDriverType = WavePlayerType.ASIO;
        }
        else if (mode == WizardMode.Software)
        {
            CheckVirtualDriver();
            // Software mode (ProMix) typically outputs to a physical device via WASAPI or ASIO
            // For now default to WASAPI as it is more common for physical outputs
            SelectedDriverType = WavePlayerType.WASAPI;
        }

        CurrentAudioSubStep = AudioSubStep.Configuration;
    }

    [RelayCommand]
    private void BackToSelection()
    {
        SelectedMode = WizardMode.NotSelected;
        CurrentAudioSubStep = AudioSubStep.Selection;
        IsAudioConfigFinished = false;
        // Stop any playing audio
        _playbackEngine.StopDevice();
    }

    [RelayCommand]
    private void ApplyAndTestConfig()
    {
        CurrentAudioSubStep = AudioSubStep.Validation;
        IsValidationRunning = true;
        ValidationMessage = "正在初始化音频引擎...";
        ValidationSuccess = false;

        try
        {
            if (SelectedAudioDevice != null)
            {
                _playbackEngine.StopDevice();
                _playbackEngine.StartDevice(SelectedAudioDevice);

                // If success
                ValidationSuccess = true;
                IsAudioConfigFinished = true;
                ValidationMessage = "配置成功";
            }
        }
        catch (Exception ex)
        {
            ValidationSuccess = false;
            ValidationMessage = $"初始化失败: {ex.Message}";
            IsAudioConfigFinished = false;
        }
        finally
        {
            IsValidationRunning = false;
        }
    }

    [RelayCommand]
    private void DownloadVirtualDriver()
    {
        try
        {
            var url = "https://vb-audio.com/Cable/";
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _toastManager.CreateToast()
                .WithTitle("无法打开链接")
                .WithContent(ex.Message)
                .OfType(NotificationType.Error)
                .Queue();
        }
    }

    [RelayCommand]
    private void RetryVirtualDriverCheck()
    {
        CheckVirtualDriver();
    }


    private async void LoadDevices()
    {
        var devices = await _audioDeviceManager.GetCachedAvailableDevicesAsync();
        UpdateDeviceList(devices);
    }

    partial void OnSelectedDriverTypeChanged(WavePlayerType value)
    {
        LoadDevices();
    }

    private void UpdateDeviceList(IReadOnlyList<DeviceDescription> allDevices)
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
        Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var devices = await _audioDeviceManager.GetCachedAvailableDevicesAsync();
            var wasapiDevices = devices.Where(d => d.WavePlayerType == WavePlayerType.WASAPI).ToList();
            IsVirtualDriverDetected = wasapiDevices.Any(d =>
                d.FriendlyName?.Contains("CABLE", StringComparison.OrdinalIgnoreCase) == true ||
                d.FriendlyName?.Contains("VoiceMeeter", StringComparison.OrdinalIgnoreCase) == true);
        });
    }
}