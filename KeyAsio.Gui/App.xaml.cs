using System.Windows;
using KeyAsio.Gui.Configuration;
using KeyAsio.Gui.Windows;

namespace KeyAsio.Gui;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private void App_OnStartup(object sender, StartupEventArgs e)
    {
        var appSettings = ConfigurationFactory.GetConfiguration<AppSettings>();

        MainWindow = new MainWindow();
        MainWindow.Show();
    }
}