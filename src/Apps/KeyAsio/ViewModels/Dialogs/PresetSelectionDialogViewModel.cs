using Avalonia.Controls.Notifications;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

    public PresetSelectionDialogViewModel(PresetManager presetManager, ISukiDialogManager dialogManager,
        ISukiToastManager toastManager, AudioSettingsViewModel audioSettingsViewModel)
    {
        _presetManager = presetManager;
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _audioSettingsViewModel = audioSettingsViewModel;
    }

    public List<PresetModel> Presets => _presetManager.AvailablePresets;

    [RelayCommand]
    public async Task SelectPreset(PresetModel preset)
    {
        await _presetManager.ApplyPreset(preset.Mode, _audioSettingsViewModel);
        _toastManager.CreateSimpleInfoToast()
            .OfType(NotificationType.Success)
            .WithTitle("已应用预设")
            .WithContent($"已成功切换到{preset.Title}")
            .Dismiss().ByClicking()
            .Queue();
        _dialogManager.DismissDialog();
    }

    [RelayCommand]
    public void Close()
    {
        _dialogManager.DismissDialog();
    }
}