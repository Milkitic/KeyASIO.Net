using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeyAsio.Services;
using KeyAsio.Shared;
using KeyAsio.Views.Dialogs;
using Microsoft.Extensions.Logging;
using Milki.Extensions.Configuration;
using Milki.Extensions.MouseKeyHook;
using SukiUI.Dialogs;

namespace KeyAsio.ViewModels;

public partial class KeyBindingViewModel : ObservableObject
{
    private readonly ILogger<KeyBindingViewModel> _logger;
    private readonly AppSettings _appSettings;
    private readonly KeyboardBindingInitializer _keyboardBindingInitializer;
    private readonly ISukiDialogManager _dialogManager;

    public KeyBindingViewModel(
        ILogger<KeyBindingViewModel> logger,
        ISukiDialogManager dialogManager,
        AppSettings appSettings,
        KeyboardBindingInitializer keyboardBindingInitializer)
    {
        _logger = logger;
        _appSettings = appSettings;
        _keyboardBindingInitializer = keyboardBindingInitializer;
        _dialogManager = dialogManager;
    }

    public ObservableCollection<HookKeys> BoundKeys
    {
        get
        {
            if (field != null) return field;

            field = new ObservableCollection<HookKeys>(_appSettings.Input.Keys);
            field.CollectionChanged += (_, _) =>
            {
                try
                {
                    _appSettings.Input.Keys = field.Distinct().ToList();
                    _appSettings.Save();
                    if (_keyboardBindingInitializer != null!)
                    {
                        _keyboardBindingInitializer.UnregisterAll();
                        _keyboardBindingInitializer.RegisterKeys(_appSettings.Input.Keys);
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Failed to save key bindings.");
                }
            };
            return field;
        }
    }

    [ObservableProperty]
    public partial bool IsEditingKeys { get; set; }

    [RelayCommand]
    public void ToggleEditKeys()
    {
        IsEditingKeys = !IsEditingKeys;
    }

    [RelayCommand]
    public void AddKey()
    {
        _dialogManager.DismissDialog();

        if (_keyboardBindingInitializer?.KeyboardHook is null)
        {
            // Should not happen if initialized correctly
            return;
        }

        var vm = new KeyBindDialogViewModel(_keyboardBindingInitializer.KeyboardHook, key =>
            {
                if (!BoundKeys.Contains(key))
                {
                    BoundKeys.Add(key);
                }
            },
            () => { _dialogManager.DismissDialog(); });

        _dialogManager.CreateDialog()
            .WithContent(new KeyBindDialogView { DataContext = vm })
            .WithTitle("Bind Key")
            .WithActionButton("Cancel", _ => vm.Dispose(), true)
            .TryShow();
    }

    [RelayCommand]
    public void RemoveKey(HookKeys key)
    {
        BoundKeys.Remove(key);
    }
}