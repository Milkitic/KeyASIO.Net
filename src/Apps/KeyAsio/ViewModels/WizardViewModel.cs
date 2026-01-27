using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeyAsio.Lang;
using KeyAsio.Shared;
using KeyAsio.ViewModels.Dialogs;

namespace KeyAsio.ViewModels;

public partial class WizardViewModel : ViewModelBase
{
    private readonly AppSettings _appSettings;

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
    public partial string PreviousButtonText { get; set; } = SRKeys.Wizard_Previous;

    [ObservableProperty]
    public partial string NextButtonText { get; set; } = SRKeys.Wizard_Next;

    [ObservableProperty]
    public partial bool AllowAutoLoadSkins { get; set; } = true;

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
        PresetSelectionViewModel = null!;
        _appSettings = null!;
    }

    public WizardViewModel(AppSettings appSettings,
        WizardAudioConfigViewModel wizardAudioConfigViewModel,
        PresetSelectionDialogViewModel presetSelectionDialogViewModel)
    {
        _appSettings = appSettings;
        WizardAudioConfigViewModel = wizardAudioConfigViewModel;

        Steps =
        [
            SR.Wizard_Title, // Welcome
            SR.Preset_SelectionTitle, // Preset
            "连接 osu!", // Game Integration
            SR.Wizard_Config_Title, // Audio Configuration
            SR.Wizard_Privacy_Title
        ];

        PresetSelectionViewModel = presetSelectionDialogViewModel;
        PresetSelectionViewModel.DismissOnSelect = false;
        PresetSelectionViewModel.ShowCloseButton = false;
        PresetSelectionViewModel.OnPresetApplied += () => { PresetAppliedMessage = "预设已应用"; };

        if (!string.IsNullOrWhiteSpace(_appSettings.Paths.OsuFolderPath))
        {
            OsuScanStatus = $"已检测到路径：{_appSettings.Paths.OsuFolderPath}";
        }

        AllowAutoLoadSkins = _appSettings.Paths.AllowAutoLoadSkins ?? true;

        // Listen to child VM changes if needed, e.g. to re-evaluate NextCommand
        WizardAudioConfigViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ViewModels.WizardAudioConfigViewModel.IsAudioConfigFinished) ||
                e.PropertyName == nameof(ViewModels.WizardAudioConfigViewModel.CanGoForward))
            {
                NextCommand.NotifyCanExecuteChanged();
            }
            else if (e.PropertyName == nameof(ViewModels.WizardAudioConfigViewModel.CurrentAudioSubStep))
            {
                UpdateNavigationState();
                NextCommand.NotifyCanExecuteChanged();
            }
            else if (e.PropertyName == nameof(ViewModels.WizardAudioConfigViewModel.ValidationSuccess))
            {
                UpdateNavigationState();
                NextCommand.NotifyCanExecuteChanged();
            }
        };
    }

    partial void OnStepIndexChanged(int value)
    {
        UpdateNavigationState();
    }

    private void UpdateNavigationState()
    {
        var prevText = SRKeys.Wizard_Previous;
        var nextText = SRKeys.Wizard_Next;

        if (StepIndex == 3)
        {
            if (WizardAudioConfigViewModel.CurrentAudioSubStep == AudioSubStep.Configuration)
            {
                prevText = SRKeys.Wizard_BackToSelection;
                nextText = SRKeys.Wizard_ApplyAndTest;
            }
            else if (WizardAudioConfigViewModel.CurrentAudioSubStep == AudioSubStep.Validation)
            {
                prevText = SRKeys.Wizard_BackToConfig;
                if (!WizardAudioConfigViewModel.ValidationSuccess)
                {
                    nextText = SRKeys.Wizard_Retry;
                }
            }
        }

        PreviousButtonText = prevText;
        NextButtonText = nextText;
    }

    public bool CanGoNext()
    {
        if (StepIndex == 3)
        {
            return WizardAudioConfigViewModel.CanGoForward;
        }

        return true;
    }

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private void Next()
    {
        if (StepIndex == 3)
        {
            if (WizardAudioConfigViewModel.TryGoForward()) return;
        }

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
        _appSettings.Paths.AllowAutoLoadSkins = AllowAutoLoadSkins;
        // _appSettings.Update.EnableAutoUpdate = EnableUpdates; // TODO: will be added

        _appSettings.General.IsFirstRun = false;
        // Trigger close window
        OnRequestClose?.Invoke();
    }

    public event Action? OnRequestClose;
}