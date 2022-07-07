using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using JetBrains.Annotations;

namespace KeyAsio.Gui.UserControls;

public class MaxButton : SystemButton, INotifyPropertyChanged
{
    private bool _isRegister;
    private bool _isWindowMax;

    public MaxButton()
    {
        Click += OnClick;
    }

    public bool IsWindowMax
    {
        get => _isWindowMax;
        private set
        {
            if (value == _isWindowMax) return;
            _isWindowMax = value;
            OnPropertyChanged();
        }
    }

    private void OnClick(object sender, RoutedEventArgs args)
    {
        var hostWindow = HostWindow;
        if (!_isRegister && hostWindow != null)
        {
            RegisterStateChanged(hostWindow);
        }

        if (hostWindow == null) return;
        if (hostWindow.WindowState == WindowState.Normal)
        {
            hostWindow.WindowState = WindowState.Maximized;
        }
        else if (hostWindow.WindowState == WindowState.Maximized)
        {
            hostWindow.WindowState = WindowState.Normal;
        }
    }

    private void RegisterStateChanged(Window window)
    {
        window.StateChanged += (_, _) =>
        {
            if (window.WindowState == WindowState.Normal)
            {
                IsWindowMax = false;
            }
            else if (window.WindowState == WindowState.Maximized)
            {
                IsWindowMax = true;
            }
        };

        _isRegister = true;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    [NotifyPropertyChangedInvocator]
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}