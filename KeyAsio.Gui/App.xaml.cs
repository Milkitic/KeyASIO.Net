using System.Windows;
using System.Windows.Controls;
using KeyAsio.Gui.Configuration;
using KeyAsio.Gui.Models;
using KeyAsio.Gui.Utils;
using KeyAsio.Gui.Windows;
using NLog.Config;
using OsuRTDataProvider.Listen;
using OrtdpLogger = OsuRTDataProvider.Logger;
using OrtdpSetting = OsuRTDataProvider.Setting;

namespace KeyAsio.Gui;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    internal readonly RichTextBox RichTextBox = new();

    private void App_OnStartup(object sender, StartupEventArgs e)
    {
        ConfigurationItemFactory
            .Default
            .Targets
            .RegisterDefinition("MemoryTarget", typeof(MemoryTarget));
        var shared = SharedViewModel.Instance;
        var settings = ConfigurationFactory.GetConfiguration<AppSettings>();

        shared.Debugging = settings.Debugging;

        if (settings.RealtimeOptions.RealtimeMode)
        {
            OrtdpLogger.SetLoggerFactory(SharedUtils.LoggerFactory);
            OrtdpSetting.ListenInterval = 3;
            var manager = new OsuListenerManager();
            manager.OnPlayingTimeChanged += playTime => RealtimeModeManager.Instance.PlayTime = playTime;
            manager.OnBeatmapChanged += beatmap => RealtimeModeManager.Instance.Beatmap = beatmap;
            manager.OnStatusChanged += (pre, current) => RealtimeModeManager.Instance.OsuStatus = current;
            manager.Start();
            RealtimeModeManager.Instance.OsuListenerManager = manager;
        }

        var miClearAll = new MenuItem
        {
            Header = "_Clear All"
        };
        RichTextBox.ContextMenu = new ContextMenu
        {
            Items = { miClearAll }
        };
        RichTextBox.Document.Blocks.Clear();
        miClearAll.Click += miClearAll_Click;
        MainWindow = new MainWindow();
        MainWindow.Show();
    }

    private void miClearAll_Click(object sender, RoutedEventArgs e)
    {
        RichTextBox.Document.Blocks.Clear();
        //TextBox.Clear();
    }
}