using System.Collections.ObjectModel;
using System.Windows;
using KeyAsio.Gui.Configuration;
using KeyAsio.Gui.Windows;
using OsuRTDataProvider.Listen;
using OsuRTDataProvider;
using OrtdpSetting = OsuRTDataProvider.Setting;

namespace KeyAsio.Gui;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public ObservableCollection<string> Logs { get; } = new();

    private void App_OnStartup(object sender, StartupEventArgs e)
    {
        var settings = ConfigurationFactory.GetConfiguration<AppSettings>();

        if (settings.OsuMode)
        {
            OrtdpSetting.ListenInterval = 1;
            var manager = new OsuListenerManager();
            manager.OnPlayingTimeChanged += playTime => SharedViewModel.Instance.PlayTime = playTime;
            manager.OnBeatmapChanged += beatmap => SharedViewModel.Instance.Beatmap = beatmap;
            manager.OnStatusChanged += (pre, current) => SharedViewModel.Instance.OsuStatus = current;
            manager.Start();
            SharedViewModel.Instance.OsuListenerManager = manager;
        }

        MainWindow = new MainWindow();
        MainWindow.Show();
    }
}