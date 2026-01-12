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

    public PresetSelectionDialogViewModel(PresetManager presetManager, ISukiDialogManager dialogManager,
        ISukiToastManager toastManager)
    {
        _presetManager = presetManager;
        _dialogManager = dialogManager;
        _toastManager = toastManager;
    }

    public List<PresetModel> Presets => _presetManager.AvailablePresets;

    [RelayCommand]
    public void SelectPreset(PresetModel preset)
    {
        _presetManager.ApplyPreset(preset.Mode);
        _toastManager.CreateToast()
            .OfType(NotificationType.Success)
            .WithTitle("已应用预设")
            .WithContent($"已成功切换到{preset.Title}")
            .Queue();
        _dialogManager.DismissDialog();
    }

    [RelayCommand]
    public void Close()
    {
        _dialogManager.DismissDialog();
    }
}