using System.Windows;

namespace KeyAsio.Gui.UserControls;

public class MinButton : SystemButton
{
    public MinButton()
    {
        Click += OnClick;
    }

    private void OnClick(object sender, RoutedEventArgs args)
    {
        if (HostWindow != null)
        {
            HostWindow.WindowState = WindowState.Minimized;
        }
    }
}