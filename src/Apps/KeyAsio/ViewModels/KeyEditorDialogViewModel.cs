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

        _keyboardHook.KeyPressed += OnKeyPressed;
    }

    public ObservableCollection<HookKeys> BoundKeys { get; }

    [ObservableProperty]
    public partial string Message { get; set; } = SR.KeyBind_Message;

    private void OnKeyPressed(HookModifierKeys hookModifierKeys, HookKeys hookKey, KeyAction type)
    {
        if (type == KeyAction.KeyDown)
        {
            // Ignore Left Button to avoid conflict with UI interactions
            if (hookKey == HookKeys.LButton) return;

            Dispatcher.UIThread.Post(() =>
            {
                if (_isDisposed) return;

                if (!BoundKeys.Contains(hookKey))
                {
                    BoundKeys.Add(hookKey);
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
        }
    }

    [RelayCommand]
    public void RemoveKey(HookKeys key)
    {
        BoundKeys.Remove(key);
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _keyboardHook.KeyPressed -= OnKeyPressed;
        GC.SuppressFinalize(this);
    }
}
