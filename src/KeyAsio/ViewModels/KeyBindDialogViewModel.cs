using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using KeyAsio.Shared;
using Milki.Extensions.MouseKeyHook;

namespace KeyAsio.ViewModels;

public partial class KeyBindDialogViewModel : ViewModelBase, IDisposable
{
    private readonly IKeyboardHook _keyboardHook;
    private readonly Action<HookKeys> _onKeyBound;
    private readonly Action _onClose;
    private bool _isDisposed;

    [ObservableProperty]
    private string _message = "Press any key to bind...";

    public KeyBindDialogViewModel(AppSettingsInput appSettingsInput, Action<HookKeys> onKeyBound, Action onClose)
    {
        _onKeyBound = onKeyBound;
        _onClose = onClose;

        _keyboardHook = appSettingsInput.UseRawInput
            ? KeyboardHookFactory.CreateRawInput()
            : KeyboardHookFactory.CreateApplication();

        _keyboardHook.KeyPressed += OnKeyPressed;
    }

    private void OnKeyPressed(HookModifierKeys hookModifierKeys, HookKeys hookKey, KeyAction type)
    {
        if (type == KeyAction.KeyDown)
        {
            // Execute on UI thread to ensure thread safety
            Dispatcher.UIThread.Post(() =>
            {
                if (_isDisposed) return;

                _onKeyBound(hookKey);
                Dispose();
                _onClose();
            });
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _keyboardHook.KeyPressed -= OnKeyPressed;
        _keyboardHook.Dispose();
        GC.SuppressFinalize(this);
    }
}