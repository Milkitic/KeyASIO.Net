using System.Windows;

namespace KeyAsio.Gui.UserControls;

public class SystemButton : CustomizableButton
{
    protected Window? HostWindow { get; private set; }

    public SystemButton()
    {
        Loaded += SystemButton_Loaded;
    }

    private void SystemButton_Loaded(object sender, RoutedEventArgs e)
    {
        if (HostWindow != null)
        {
            return;
        }

        HostWindow = Window.GetWindow(this);
    }

    static SystemButton()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(SystemButton), new FrameworkPropertyMetadata(typeof(SystemButton)));
    }
}