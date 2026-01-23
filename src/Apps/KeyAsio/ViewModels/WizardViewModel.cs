using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeyAsio.Lang;
using KeyAsio.Services;
using KeyAsio.Shared;
using KeyAsio.ViewModels.Dialogs;
using SukiUI.Dialogs;
using SukiUI.Toasts;

namespace KeyAsio.ViewModels;

public partial class WizardViewModel : ViewModelBase
{
    private readonly ISukiDialogManager _dialogManager;
    private readonly AppSettings _appSettings;
    private readonly PresetManager _presetManager;
    private readonly ISukiToastManager _toastManager;

    public WizardAudioConfigViewModel WizardAudioConfigViewModel { get; }

    [ObservableProperty]
    public partial int StepIndex { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<string> Steps { get; set; }

    // Privacy
    [ObservableProperty]
    public partial bool EnableCrashReport { get; set; } = true;

    [ObservableProperty]
    public partial bool EnableUpdates { get; set; } = true;

    [ObservableProperty]
    public partial PresetSelectionDialogViewModel? PresetSelectionViewModel { get; set; }

    [ObservableProperty]
    public partial string PresetAppliedMessage { get; set; } = "";

    [ObservableProperty]
    public partial string OsuScanStatus { get; set; } = "正在扫描 osu! 进程...";

    [ObservableProperty]
    public partial bool EnableSyncOnLaunch { get; set; } = true;

    [RelayCommand]
    private async Task BrowseOsuExecutable()
    {
        var topLevel = TopLevel.GetTopLevel(
            Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null);
        if (topLevel == null) return;

        var result = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择 osu!.exe",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("osu! executable")
                {
                    Patterns = ["osu!.exe"],
                    MimeTypes = ["application/vnd.microsoft.portable-executable"]
                }
            ]
        });

        if (result.Count > 0)
        {
            var filePath = result[0].Path.LocalPath;
            var folder = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(folder))
            {
                _appSettings.Paths.OsuFolderPath = folder;
                OsuScanStatus = $"已选择路径：{folder}";
            }
        }
    }

    public WizardViewModel()
    {
        if (!Design.IsDesignMode)
            throw new InvalidOperationException("This constructor is only for design-time purposes.");
        Steps =
        [
            SR.Wizard_Title, // Welcome
            SR.Wizard_Config_Title, // Configuration
            SR.Wizard_Privacy_Title
        ];
        WizardAudioConfigViewModel = null!;
    }

    public WizardViewModel(
        AudioSettingsViewModel audioSettingsViewModel,
        ISukiDialogManager dialogManager,
        AppSettings appSettings,
        PresetManager presetManager,
        ISukiToastManager toastManager,
        WizardAudioConfigViewModel wizardAudioConfigViewModel)
    {
        _dialogManager = dialogManager;
        _appSettings = appSettings;
        _presetManager = presetManager;
        _toastManager = toastManager;
        WizardAudioConfigViewModel = wizardAudioConfigViewModel;

        Steps =
        [
            SR.Wizard_Title, // Welcome
            SR.Preset_SelectionTitle, // Preset
            "连接 osu!", // Game Integration
            SR.Wizard_Config_Title, // Audio Configuration
            SR.Wizard_Privacy_Title
        ];

        PresetSelectionViewModel =
            new PresetSelectionDialogViewModel(_presetManager, _dialogManager, _toastManager, audioSettingsViewModel)
            {
                DismissOnSelect = false,
                ShowCloseButton = false
            };
        PresetSelectionViewModel.OnPresetApplied += () => { PresetAppliedMessage = "预设已应用"; };

        if (!string.IsNullOrWhiteSpace(_appSettings.Paths.OsuFolderPath))
        {
            OsuScanStatus = $"已检测到路径：{_appSettings.Paths.OsuFolderPath}";
        }

        // Listen to child VM changes if needed, e.g. to re-evaluate NextCommand
        WizardAudioConfigViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ViewModels.WizardAudioConfigViewModel.IsAudioConfigFinished))
            {
                NextCommand.NotifyCanExecuteChanged();
            }
        };
    }

    public bool CanGoNext()
    {
        if (StepIndex == 3 && !WizardAudioConfigViewModel.IsAudioConfigFinished)
        {
            return false;
        }

        return true;
    }

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private async Task Next()
    {
        if (StepIndex < Steps.Count - 1)
        {
            StepIndex++;
        }
        else
        {
            Finish();
        }
    }

    [RelayCommand]
    private void Previous()
    {
        if (StepIndex == 3)
        {
            if (WizardAudioConfigViewModel.TryGoBack())
            {
                return;
            }
        }

        if (StepIndex > 0)
        {
            StepIndex--;
        }
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
        _appSettings.Sync.EnableSync = EnableSyncOnLaunch;
        // _appSettings.Update.EnableAutoUpdate = EnableUpdates; // Assuming this exists or will be added

        _appSettings.General.IsFirstRun = false;
        // Trigger close window
        OnRequestClose?.Invoke();
    }

    public event Action? OnRequestClose;
}