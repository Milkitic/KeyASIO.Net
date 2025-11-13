using System.Windows;
using KeyAsio.MemoryReading;
using KeyAsio.Shared;
using KeyAsio.Shared.Models;
using KeyAsio.Shared.Realtime;
using OrtdpLogger = KeyAsio.MemoryReading.Logger;

namespace MemoryReadingTest
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private readonly RealtimeModeManager _realtime = new(new SharedViewModel());

        private void App_OnStartup(object sender, StartupEventArgs e)
        {
            OrtdpLogger.SetLoggerFactory(LogUtils.LoggerFactory);
            MemoryScan.MemoryReadObject.PlayerNameChanged += (_, player) => _realtime.Username = player;
            MemoryScan.MemoryReadObject.ModsChanged += (_, mods) => _realtime.PlayMods = mods;
            MemoryScan.MemoryReadObject.ComboChanged += (_, combo) => _realtime.Combo = combo;
            MemoryScan.MemoryReadObject.ScoreChanged += (_, score) => _realtime.Score = score;
            MemoryScan.MemoryReadObject.PlayingTimeChanged += (_, playTime) => _realtime.LastFetchedPlayTime = playTime;
            MemoryScan.MemoryReadObject.BeatmapIdentifierChanged += (_, beatmap) => _realtime.Beatmap = beatmap;
            MemoryScan.MemoryReadObject.OsuStatusChanged += (pre, current) => _realtime.OsuStatus = current;
            MemoryScan.Start(100, 10);
        }
    }
}