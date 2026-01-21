using Avalonia.Controls.Notifications;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeyAsio.Lang;
using KeyAsio.Services;
using SukiUI.Dialogs;
using SukiUI.Toasts;

namespace KeyAsio.ViewModels.Dialogs;

public partial class PresetSelectionDialogViewModel : ObservableObject
{
    private readonly PresetManager _presetManager;
    private readonly ISukiDialogManager _dialogManager;
    private readonly ISukiToastManager _toastManager;
    private readonly AudioSettingsViewModel _audioSettingsViewModel;
    public bool DismissOnSelect { get; set; } = true;
    public bool ShowCloseButton { get; set; } = true;
    public event Action? OnPresetApplied;

    [ObservableProperty]
    private PresetMode? _currentPresetMode;

    public PresetSelectionDialogViewModel(PresetManager presetManager, ISukiDialogManager dialogManager,
        ISukiToastManager toastManager, AudioSettingsViewModel audioSettingsViewModel)
    {
        _presetManager = presetManager;
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _audioSettingsViewModel = audioSettingsViewModel;

        CurrentPresetMode = _presetManager.GetCurrentPresetMode();
    }

    public List<PresetModel> Presets => _presetManager.AvailablePresets;

    [RelayCommand]
    public async Task SelectPreset(PresetModel preset)
    {
        await _presetManager.ApplyPreset(preset.Mode, _audioSettingsViewModel);

        CurrentPresetMode = preset.Mode;

        _toastManager.CreateSimpleInfoToast()
            .OfType(NotificationType.Success)
            .WithTitle(SR.Preset_AppliedTitle)
            .WithContent(string.Format(SR.Preset_AppliedContent, SR.ResourceManager.GetString(preset.Title)))
            .Dismiss().ByClicking()
            .Queue();
        if (DismissOnSelect)
        {
            _dialogManager.DismissDialog();
        }
        else
        {
            OnPresetApplied?.Invoke();
        }
    }

    [RelayCommand]
    public void Close()
    {
        _dialogManager.DismissDialog();
    }
}