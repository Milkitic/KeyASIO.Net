using System.Collections.ObjectModel;
using System.Windows;
using KeyAsio.Gui.Configuration;
using KeyAsio.Gui.Windows;

namespace KeyAsio.Gui;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public ObservableCollection<string> Logs { get; } = new();

    private void App_OnStartup(object sender, StartupEventArgs e)
    {
        ConfigurationFactory.GetConfiguration<AppSettings>();

        MainWindow = new MainWindow();
        MainWindow.Show();
    }
}