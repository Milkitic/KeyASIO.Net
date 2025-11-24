using System.Windows;
using KeyAsio.MemoryReading;
using KeyAsio.Shared;
using KeyAsio.Shared.Models;
using KeyAsio.Shared.Realtime;
using KeyAsio.Shared.Realtime.Services;
using Microsoft.Extensions.DependencyInjection;

namespace MemoryReadingTest
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private readonly RealtimeSessionContext _realtimeSessionContext;

        public App()
        {
            var services = new ServiceCollection();
            services.AddSingleton(new YamlAppSettings());
            services.AddSingleton<SharedViewModel>();
            services.AddSingleton<AudioCacheService>();
            services.AddSingleton<MemoryScan>();
            services.AddSingleton<BeatmapHitsoundLoader>();
            services.AddSingleton<BackgroundMusicManager>();
            services.AddSingleton<SfxPlaybackService>();
            services.AddSingleton<RealtimeController>();
            services.AddSingleton<RealtimeSessionContext>();
            var provider = services.BuildServiceProvider();
            _realtimeSessionContext = provider.GetRequiredService<RealtimeSessionContext>();
            MemoryScan = provider.GetRequiredService<MemoryScan>();
        }

        public MemoryScan MemoryScan { get; }

        private void App_OnStartup(object sender, StartupEventArgs e)
        {
            MemoryScan.MemoryReadObject.PlayerNameChanged += (_, player) => _realtimeSessionContext.Username = player;
            MemoryScan.MemoryReadObject.ModsChanged += (_, mods) => _realtimeSessionContext.PlayMods = mods;
            MemoryScan.MemoryReadObject.ComboChanged += (_, combo) => _realtimeSessionContext.Combo = combo;
            MemoryScan.MemoryReadObject.ScoreChanged += (_, score) => _realtimeSessionContext.Score = score;
            MemoryScan.MemoryReadObject.PlayingTimeChanged += (_, playTime) => _realtimeSessionContext.BaseMemoryTime = playTime;
            MemoryScan.MemoryReadObject.BeatmapIdentifierChanged += (_, beatmap) => _realtimeSessionContext.Beatmap = beatmap;
            MemoryScan.MemoryReadObject.OsuStatusChanged += (pre, current) => _realtimeSessionContext.OsuStatus = current;
            MemoryScan.Start(100, 10);
        }
    }
}