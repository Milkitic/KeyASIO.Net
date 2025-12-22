using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeyAsio.Views.Dialogs;
using Milki.Extensions.Configuration;
using Milki.Extensions.MouseKeyHook;
using SukiUI.Dialogs;

namespace KeyAsio.ViewModels;

public partial class MainWindowViewModel
{
    public ObservableCollection<HookKeys> BoundKeys
    {
        get
        {
            if (field != null) return field;

            field = new ObservableCollection<HookKeys>(AppSettings.Input.Keys);
            field.CollectionChanged += (_, _) =>
            {
                try
                {
                    AppSettings.Input.Keys = field.Distinct().ToList();
                    AppSettings.Save();
                    if (_keyboardBindingInitializer != null)
                    {
                        _keyboardBindingInitializer.UnregisterAll();
                        _keyboardBindingInitializer.RegisterKeys(AppSettings.Input.Keys);
                    }
                }
                catch (Exception e)
                {
                    // Ignore or log
                    System.Diagnostics.Debug.WriteLine(e);
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
        DialogManager.DismissDialog();

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
            () => { DialogManager.DismissDialog(); });

        DialogManager.CreateDialog()
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