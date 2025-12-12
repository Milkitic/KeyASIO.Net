using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Milki.Extensions.MouseKeyHook;

namespace KeyAsio.ViewModels;

public partial class KeyBindDialogViewModel : ViewModelBase, IDisposable
{
    private readonly IKeyboardHook _keyboardHook;
    private readonly Action<HookKeys> _onKeyBound;
    private readonly Action _onClose;
    private bool _isDisposed;

    public KeyBindDialogViewModel(IKeyboardHook keyboardHook, Action<HookKeys> onKeyBound, Action onClose)
    {
        _keyboardHook = keyboardHook;
        _onKeyBound = onKeyBound;
        _onClose = onClose;

        _keyboardHook.KeyPressed += OnKeyPressed;
    }

    [ObservableProperty]
    public partial string Message { get; set; } = "Press any key to bind...";

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
        GC.SuppressFinalize(this);
    }
}