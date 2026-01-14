using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeyAsio.Lang;
using Milki.Extensions.MouseKeyHook;

namespace KeyAsio.ViewModels;

public partial class KeyEditorDialogViewModel : ViewModelBase, IDisposable
{
    private readonly IKeyboardHook _keyboardHook;
    private bool _isDisposed;

    public KeyEditorDialogViewModel(IKeyboardHook keyboardHook, ObservableCollection<HookKeys> boundKeys)
    {
        _keyboardHook = keyboardHook;
        BoundKeys = boundKeys;

        foreach (var key in boundKeys)
        {
            DisplayKeys.Add(new KeyItemWrapper(key));
        }

        _keyboardHook.KeyPressed += OnKeyPressed;
    }

    public ObservableCollection<HookKeys> BoundKeys { get; }
    public ObservableCollection<KeyItemWrapper> DisplayKeys { get; } = new();

    [ObservableProperty]
    public partial string Message { get; set; } = SR.KeyBinding_Message;

    private void OnKeyPressed(HookModifierKeys hookModifierKeys, HookKeys hookKey, KeyAction type)
    {
        if (type == KeyAction.KeyDown)
        {
            // Ignore Left Button to avoid conflict with UI interactions
            if (hookKey == HookKeys.LButton) return;

            Dispatcher.UIThread.Post(() =>
            {
                if (_isDisposed) return;

                var wrapper = DisplayKeys.FirstOrDefault(x => x.Key == hookKey);
                if (wrapper != null)
                {
                    wrapper.Trigger();
                }
                else if (!BoundKeys.Contains(hookKey))
                {
                    BoundKeys.Add(hookKey);
                    var newWrapper = new KeyItemWrapper(hookKey);
                    DisplayKeys.Add(newWrapper);
                    newWrapper.Trigger();
                }
            });
        }
    }

    [RelayCommand]
    public void BindMouseLeft()
    {
        if (!BoundKeys.Contains(HookKeys.LButton))
        {
            BoundKeys.Add(HookKeys.LButton);
            DisplayKeys.Add(new KeyItemWrapper(HookKeys.LButton));
        }
    }

    [RelayCommand]
    public void RemoveKey(KeyItemWrapper wrapper)
    {
        BoundKeys.Remove(wrapper.Key);
        DisplayKeys.Remove(wrapper);
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _keyboardHook.KeyPressed -= OnKeyPressed;
        GC.SuppressFinalize(this);
    }
}

public partial class KeyItemWrapper : ObservableObject
{
    public KeyItemWrapper(HookKeys key)
    {
        Key = key;
    }

    public HookKeys Key { get; }

    [ObservableProperty]
    private bool _isActive;

    public void Trigger()
    {
        IsActive = true;
        Dispatcher.UIThread.Post(async () =>
        {
            await Task.Delay(20);
            IsActive = false;
        });
    }
}