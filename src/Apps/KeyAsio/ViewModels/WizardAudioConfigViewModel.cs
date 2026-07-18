using System.Collections.ObjectModel;
using Avalonia.Controls.Notifications;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeyAsio.Configuration;
using KeyAsio.Core.Audio;
using KeyAsio.Plugins.Contracts;
using KeyAsio.Services;
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
    ConcurrencyCheck,
    AlternativeDeviceCheck,
    ProMixRequired,
    Validation
}

public partial class WizardAudioConfigViewModel : ViewModelBase
{
    private const string ProMixPluginId = "KeyAsio.Plugins.ProMix";

    private readonly IAudioDeviceManager _audioDeviceManager;
    private readonly IAudioDeviceOperationCoordinator _deviceOperations;
    private readonly IWizardTestSoundService _testSoundService;
    private readonly ISukiToastManager _toastManager;
    private readonly AppSettings _appSettings;
    private IReadOnlyList<DeviceDescription> _allAudioDevices = [];

    public WizardAudioConfigViewModel(
        IAudioDeviceManager audioDeviceManager,
        IAudioDeviceOperationCoordinator deviceOperations,
        ISukiToastManager toastManager,
        AppSettings appSettings,
        IPluginManager pluginManager,
        IWizardTestSoundService testSoundService)
    {
        _audioDeviceManager = audioDeviceManager;
        _deviceOperations = deviceOperations;
        _testSoundService = testSoundService;
        _toastManager = toastManager;
        _appSettings = appSettings;
        IsProMixAvailable = pluginManager.GetAllPlugins()
            .Any(plugin => string.Equals(plugin.Id, ProMixPluginId, StringComparison.Ordinal));

        AvailableDriverTypes = [WavePlayerType.ASIO, WavePlayerType.WASAPI];
        SelectedDriverType = WavePlayerType.ASIO;

        _ = LoadDevicesAsync();
    }

    public bool IsProMixAvailable { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsHardwareConfig))]
    [NotifyPropertyChangedFor(nameof(IsSoftwareConfig))]
    [NotifyPropertyChangedFor(nameof(IsHardwareMode))]
    [NotifyPropertyChangedFor(nameof(CanGoForward))]
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
    [NotifyPropertyChangedFor(nameof(IsConcurrencyCheck))]
    [NotifyPropertyChangedFor(nameof(IsAlternativeDeviceCheck))]
    [NotifyPropertyChangedFor(nameof(IsProMixRequired))]
    [NotifyPropertyChangedFor(nameof(IsValidationStep))]
    [NotifyPropertyChangedFor(nameof(CanGoForward))]
    public partial AudioSubStep CurrentAudioSubStep { get; set; } = AudioSubStep.Selection;

    public bool IsSelectionMode => CurrentAudioSubStep == AudioSubStep.Selection;

    public bool IsHardwareConfig =>
        CurrentAudioSubStep == AudioSubStep.Configuration && SelectedMode == WizardMode.Hardware;

    public bool IsSoftwareConfig =>
        CurrentAudioSubStep == AudioSubStep.Configuration && SelectedMode == WizardMode.Software;

    public bool IsConcurrencyCheck => CurrentAudioSubStep == AudioSubStep.ConcurrencyCheck;

    public bool IsAlternativeDeviceCheck => CurrentAudioSubStep == AudioSubStep.AlternativeDeviceCheck;

    public bool IsProMixRequired => CurrentAudioSubStep == AudioSubStep.ProMixRequired;

    public bool IsHardwareMode => SelectedMode == WizardMode.Hardware;

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

    [ObservableProperty]
    public partial string ValidationInstruction { get; set; } =
        "请在 osu! 选图页播放音乐，并按键确认自动音乐与软件音效均正常。";

    [ObservableProperty]
    public partial string ProMixRequiredMessage { get; set; } = "";

    [ObservableProperty]
    public partial bool HasAlternativeGameDevices { get; set; }

    [ObservableProperty]
    public partial bool IsConcurrencyTestSoundPlaying { get; set; }


    public async Task<bool> TryGoBackAsync()
    {
        if (CurrentAudioSubStep == AudioSubStep.Configuration)
        {
            await BackToSelection();
            return true;
        }

        if (CurrentAudioSubStep is AudioSubStep.ConcurrencyCheck
            or AudioSubStep.AlternativeDeviceCheck
            or AudioSubStep.ProMixRequired
            or AudioSubStep.Validation)
        {
            await ReturnToConfigurationAsync();
            return true;
        }

        return false;
    }

    public async Task<bool> TryGoForwardAsync()
    {
        if (CurrentAudioSubStep == AudioSubStep.Configuration)
        {
            await ApplyAndTestConfig();
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
                await ApplyAndTestConfig();
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
            if (IsSoftwareConfig) return SelectedAudioDevice != null;
            if (IsValidationStep) return true; // Can always retry or proceed if success
            return false;
        }
    }

    public void StopTestSound()
    {
        _testSoundService.Stop();
        IsConcurrencyTestSoundPlaying = false;
    }

    [ObservableProperty]
    public partial bool ShowVirtualDriverWarning { get; set; }

    [ObservableProperty]
    public partial string VirtualDriverWarning { get; set; } = "";

    private bool CanSelectMode(WizardMode mode) => mode != WizardMode.Software || IsProMixAvailable;

    [RelayCommand(CanExecute = nameof(CanSelectMode))]
    private void SelectMode(WizardMode mode)
    {
        StopTestSound();
        SelectedMode = mode;
        if (mode == WizardMode.Hardware)
        {
            _appSettings.Sync.EnableMixSync = false;
            SelectedDriverType = WavePlayerType.ASIO;
            UpdateDeviceList(_allAudioDevices);
        }
        else if (mode == WizardMode.Software)
        {
            _appSettings.Sync.EnableMixSync = true;
            CheckVirtualDriver();
            SelectedDriverType = WavePlayerType.WASAPI;
            UpdateDeviceList(_allAudioDevices);
        }

        CurrentAudioSubStep = AudioSubStep.Configuration;
    }

    [RelayCommand]
    private async Task BackToSelection()
    {
        StopTestSound();
        SelectedMode = WizardMode.NotSelected;
        CurrentAudioSubStep = AudioSubStep.Selection;
        IsAudioConfigFinished = false;
        ValidationSuccess = false;
        await _deviceOperations.DeactivateAsync();
    }

    [RelayCommand]
    private async Task ApplyAndTestConfig()
    {
        StopTestSound();
        CurrentAudioSubStep = AudioSubStep.Validation;
        IsValidationRunning = true;
        ValidationMessage = "正在初始化音频引擎...";
        ValidationSuccess = false;

        if (SelectedAudioDevice is null)
        {
            ValidationMessage = "请选择音频设备";
            IsValidationRunning = false;
            return;
        }

        var result = await _deviceOperations.ApplyAsync(SelectedAudioDevice, _appSettings.Audio.SampleRate);
        if (result.IsSuccess)
        {
            if (SelectedMode == WizardMode.Hardware)
            {
                try
                {
                    _testSoundService.Start();
                    IsConcurrencyTestSoundPlaying = true;
                    CurrentAudioSubStep = AudioSubStep.ConcurrencyCheck;
                    ValidationMessage = "独占设备已创建";
                }
                catch (Exception exception)
                {
                    ValidationSuccess = false;
                    ValidationMessage = $"测试音播放失败: {exception.Message}";
                }
            }
            else
            {
                ValidationSuccess = true;
                IsAudioConfigFinished = true;
                ValidationMessage = "配置成功";
                ValidationInstruction =
                    "请在 osu! 中选择虚拟声卡并进入选图页，确认音乐可自动播放，再按键确认软件音效正常。";
            }
        }
        else
        {
            ValidationSuccess = false;
            ValidationMessage = $"初始化失败: {result.Error?.Message ?? "未知错误"}";
            IsAudioConfigFinished = false;
        }

        IsValidationRunning = false;
    }

    [RelayCommand]
    private void ConfirmSameDeviceAudio()
    {
        CompleteHardwareRouting(
            "设备配置完成",
            "保持 osu! 使用当前设备，将 osu! 的全局延迟调整至 -40ms 左右，选择一张谱面使用 Auto 进行游玩，确认软件音效与游戏音乐正常工作。");
    }

    [RelayCommand]
    private async Task ReportSameDeviceSilent()
    {
        if (HasAlternativeGameDevices)
        {
            CurrentAudioSubStep = AudioSubStep.AlternativeDeviceCheck;
            return;
        }

        await RequireProMixAsync("系统只检测到一个物理播放设备，而且它不支持 ASIO/WASAPI 并发。");
    }

    [RelayCommand]
    private void ConfirmAlternativeDeviceAudio()
    {
        CompleteHardwareRouting(
            "设备配置完成",
            "保持 osu! 使用当前有声音的设备，选择一张谱面使用 Auto 进行游玩，确认软件音效与游戏音乐在两个设备上正常工作。\n\n注意：你需要进行自主 DIY（例如使用外部混音器设备），这样才能统一输出到耳机中。");
    }

    [RelayCommand]
    private async Task ReportNoAlternativeDevice()
    {
        await RequireProMixAsync("没有找到可供 osu! 正常播放的其他设备，当前硬件组合无法完成手动分流。");
    }

    [RelayCommand]
    private async Task RetryHardwareSetup()
    {
        await ReturnToConfigurationAsync();
    }

    [RelayCommand(CanExecute = nameof(IsProMixAvailable))]
    private async Task SwitchToProMix()
    {
        StopTestSound();
        await _deviceOperations.DeactivateAsync();

        SelectedMode = WizardMode.Software;
        _appSettings.Sync.EnableMixSync = true;
        SelectedDriverType = WavePlayerType.WASAPI;
        UpdateDeviceList(_allAudioDevices);
        CheckVirtualDriver();
        CurrentAudioSubStep = AudioSubStep.Configuration;
    }

    private void CompleteHardwareRouting(string title, string instruction)
    {
        StopTestSound();
        ValidationSuccess = true;
        IsAudioConfigFinished = true;
        ValidationMessage = title;
        ValidationInstruction = instruction;
        CurrentAudioSubStep = AudioSubStep.Validation;
    }

    private async Task RequireProMixAsync(string message)
    {
        StopTestSound();
        await _deviceOperations.DeactivateAsync();
        ProMixRequiredMessage = message;
        ValidationSuccess = false;
        IsAudioConfigFinished = false;
        CurrentAudioSubStep = AudioSubStep.ProMixRequired;
    }

    private async Task ReturnToConfigurationAsync()
    {
        StopTestSound();
        IsValidationRunning = false;
        ValidationSuccess = false;
        IsAudioConfigFinished = false;
        await _deviceOperations.DeactivateAsync();
        CurrentAudioSubStep = AudioSubStep.Configuration;
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


    private async Task LoadDevicesAsync()
    {
        var devices = await _audioDeviceManager.GetCachedAvailableDevicesAsync();
        _allAudioDevices = devices;
        HasAlternativeGameDevices = devices
            .Where(device => device.WavePlayerType == WavePlayerType.WASAPI && device.DeviceId is not null)
            .Select(device => device.DeviceId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count() > 1;
        UpdateDeviceList(devices);
    }

    partial void OnSelectedDriverTypeChanged(WavePlayerType value)
    {
        UpdateDeviceList(_allAudioDevices);
    }

    private void UpdateDeviceList(IReadOnlyList<DeviceDescription> allDevices)
    {
        var filtered = allDevices
            .Where(device => device.WavePlayerType == SelectedDriverType)
            .Where(device => SelectedMode != WizardMode.Hardware ||
                             SelectedDriverType != WavePlayerType.WASAPI ||
                             device.DeviceId is not null)
            .Select(device => SelectedMode == WizardMode.Hardware &&
                              device.WavePlayerType == WavePlayerType.WASAPI
                ? device with { IsExclusive = true, Latency = 3 }
                : device)
            .ToList();

        AvailableAudioDevices = new ObservableCollection<DeviceDescription>(filtered);
        SelectedAudioDevice = AvailableAudioDevices.FirstOrDefault();

        if (SelectedMode == WizardMode.Hardware)
        {
            ShowHardwareDriverWarning = AvailableAudioDevices.Count == 0;
            HardwareDriverWarning = SelectedDriverType == WavePlayerType.ASIO
                ? "未检测到 ASIO 驱动，请切换到 WASAPI 独占。"
                : "未检测到可用的 WASAPI 播放设备，请尝试 ASIO 或 ProMix。";
        }
        else
        {
            ShowHardwareDriverWarning = false;
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

            if (!IsVirtualDriverDetected)
            {
                ShowVirtualDriverWarning = true;
                VirtualDriverWarning = "未检测到虚拟声卡驱动，建议安装以获得最佳体验";
            }
            else
            {
                ShowVirtualDriverWarning = false;
            }
        });
    }
}
