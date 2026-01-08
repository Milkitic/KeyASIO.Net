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

        UpdateBoundKeys();
    }

    public List<string> Modes { get; } = new()
    {
        "osu!", "osu!taiko", "osu!catch",
        "osu!mania 4K", "osu!mania 5K", "osu!mania 6K", "osu!mania 7K",
        "osu!mania 8K", "osu!mania 9K", "osu!mania 10K"
    };

    [ObservableProperty]
    public partial string SelectedMode { get; set; } = "osu!";

    partial void OnSelectedModeChanged(string value)
    {
        UpdateBoundKeys();
    }

    [ObservableProperty]
    private ObservableCollection<HookKeys> _boundKeys = new();

    private void UpdateBoundKeys()
    {
        var list = GetCurrentKeyList();
        var collection = new ObservableCollection<HookKeys>(list);
        collection.CollectionChanged += (_, _) =>
        {
            try
            {
                SaveCurrentKeyList(collection.Distinct().ToList());
                _appSettings.Save();
                if (_keyboardBindingInitializer != null!)
                {
                    _keyboardBindingInitializer.UnregisterAll();
                    _keyboardBindingInitializer.RegisterAllKeys();
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to save key bindings.");
            }
        };
        BoundKeys = collection;
    }

    private List<HookKeys> GetCurrentKeyList()
    {
        return SelectedMode switch
        {
            "osu!" => _appSettings.Input.OsuKeys,
            "osu!taiko" => _appSettings.Input.TaikoKeys,
            "osu!catch" => _appSettings.Input.CatchKeys,
            var m when m.StartsWith("osu!mania") => GetManiaKeys(m),
            _ => new List<HookKeys>()
        };
    }

    private void SaveCurrentKeyList(List<HookKeys> keys)
    {
        switch (SelectedMode)
        {
            case "osu!": _appSettings.Input.OsuKeys = keys; break;
            case "osu!taiko": _appSettings.Input.TaikoKeys = keys; break;
            case "osu!catch": _appSettings.Input.CatchKeys = keys; break;
            case var m when m.StartsWith("osu!mania"): SaveManiaKeys(m, keys); break;
        }
    }

    private List<HookKeys> GetManiaKeys(string mode)
    {
        var keyCount = int.Parse(mode.Split(' ').Last().TrimEnd('K'));
        if (!_appSettings.Input.ManiaKeys.TryGetValue(keyCount, out var keys))
        {
            keys = new List<HookKeys>();
            _appSettings.Input.ManiaKeys[keyCount] = keys;
        }

        return keys;
    }

    private void SaveManiaKeys(string mode, List<HookKeys> keys)
    {
        var keyCount = int.Parse(mode.Split(' ').Last().TrimEnd('K'));
        _appSettings.Input.ManiaKeys[keyCount] = keys;
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