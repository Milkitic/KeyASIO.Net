using System.Windows;

namespace KeyAsio.Gui.UserControls;

public class CloseButton : SystemButton
{
    public CloseButton()
    {
        Click += OnClick;
    }

    private void OnClick(object sender, RoutedEventArgs args)
    {
        HostWindow?.Close();
    }
}