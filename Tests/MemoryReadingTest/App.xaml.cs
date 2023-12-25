using System.Configuration;
using System.Data;
using System.Windows;
using KeyAsio.MemoryReading;
using KeyAsio.Shared;
using KeyAsio.Shared.Realtime;
using OrtdpLogger = KeyAsio.MemoryReading.Logger;

namespace MemoryReadingTest
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void App_OnStartup(object sender, StartupEventArgs e)
        {
            OrtdpLogger.SetLoggerFactory(LogUtils.LoggerFactory);
            MemoryScan.MemoryReadObject.PlayerNameChanged += (_, player) => RealtimeModeManager.Instance.Username = player;
            MemoryScan.MemoryReadObject.ModsChanged += (_, mods) => RealtimeModeManager.Instance.PlayMods = mods;
            MemoryScan.MemoryReadObject.ComboChanged += (_, combo) => RealtimeModeManager.Instance.Combo = combo;
            MemoryScan.MemoryReadObject.ScoreChanged += (_, score) => RealtimeModeManager.Instance.Score = score;
            MemoryScan.MemoryReadObject.PlayingTimeChanged += (_, playTime) => RealtimeModeManager.Instance.LastFetchedPlayTime = playTime;
            MemoryScan.MemoryReadObject.BeatmapIdentifierChanged += (_, beatmap) => RealtimeModeManager.Instance.Beatmap = beatmap;
            MemoryScan.MemoryReadObject.OsuStatusChanged += (pre, current) => RealtimeModeManager.Instance.OsuStatus = current;
            MemoryScan.Start(100, 10);
        }
    }
}
