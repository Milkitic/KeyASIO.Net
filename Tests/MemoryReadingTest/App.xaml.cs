using System.Windows;
using KeyAsio.MemoryReading;
using KeyAsio.Shared;
using KeyAsio.Shared.Models;
using KeyAsio.Shared.Realtime;
using KeyAsio.Shared.Realtime.Services;
using Microsoft.Extensions.DependencyInjection;
using OrtdpLogger = KeyAsio.MemoryReading.Logger;

namespace MemoryReadingTest
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private readonly RealtimeProperties _realtimeProperties;

        public App()
        {
            var services = new ServiceCollection();
            services.AddSingleton(LogUtils.GetLogger("MemoryReadingTest"));
            services.AddSingleton(new AppSettings());
            services.AddSingleton<SharedViewModel>();
            services.AddSingleton<AudioCacheService>();
            services.AddSingleton<HitsoundNodeService>();
            services.AddSingleton<MusicTrackService>();
            services.AddSingleton<AudioPlaybackService>();
            services.AddSingleton<RealtimeModeManager>();
            services.AddSingleton<RealtimeProperties>();
            var provider = services.BuildServiceProvider();
            _realtimeProperties = provider.GetRequiredService<RealtimeProperties>();
        }

        private void App_OnStartup(object sender, StartupEventArgs e)
        {
            OrtdpLogger.SetLoggerFactory(LogUtils.LoggerFactory);
            MemoryScan.MemoryReadObject.PlayerNameChanged += (_, player) => _realtimeProperties.Username = player;
            MemoryScan.MemoryReadObject.ModsChanged += (_, mods) => _realtimeProperties.PlayMods = mods;
            MemoryScan.MemoryReadObject.ComboChanged += (_, combo) => _realtimeProperties.Combo = combo;
            MemoryScan.MemoryReadObject.ScoreChanged += (_, score) => _realtimeProperties.Score = score;
            MemoryScan.MemoryReadObject.PlayingTimeChanged += (_, playTime) => _realtimeProperties.LastFetchedPlayTime = playTime;
            MemoryScan.MemoryReadObject.BeatmapIdentifierChanged += (_, beatmap) => _realtimeProperties.Beatmap = beatmap;
            MemoryScan.MemoryReadObject.OsuStatusChanged += (pre, current) => _realtimeProperties.OsuStatus = current;
            MemoryScan.Start(100, 10);
        }
    }
}