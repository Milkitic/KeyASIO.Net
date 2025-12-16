using System.Windows;
using KeyAsio.Shared;
using KeyAsio.Shared.Models;
using KeyAsio.Shared.OsuMemory;
using KeyAsio.Shared.Sync;
using KeyAsio.Shared.Sync.Services;
using Microsoft.Extensions.DependencyInjection;
using NLog.Extensions.Logging;

namespace MemoryReadingTest
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private readonly SyncSessionContext _syncSessionContext;

        public App()
        {
            var services = new ServiceCollection();
            services.AddNLog();
            services.AddSingleton(new AppSettings());
            services.AddSingleton<SharedViewModel>();
            services.AddSingleton<GameplayAudioService>();
            services.AddSingleton<MemoryScan>();
            services.AddSingleton<BeatmapHitsoundLoader>();
            services.AddSingleton<BackgroundMusicManager>();
            services.AddSingleton<SfxPlaybackService>();
            services.AddSingleton<SyncController>();
            services.AddSingleton<SyncSessionContext>();
            var provider = services.BuildServiceProvider();
            _syncSessionContext = provider.GetRequiredService<SyncSessionContext>();
            MemoryScan = provider.GetRequiredService<MemoryScan>();
        }

        public MemoryScan MemoryScan { get; }

        private void App_OnStartup(object sender, StartupEventArgs e)
        {
            MemoryScan.MemoryReadObject.PlayerNameChanged += (_, player) => _syncSessionContext.Username = player;
            MemoryScan.MemoryReadObject.ModsChanged += (_, mods) => _syncSessionContext.PlayMods = mods;
            MemoryScan.MemoryReadObject.ComboChanged += (_, combo) => _syncSessionContext.Combo = combo;
            MemoryScan.MemoryReadObject.ScoreChanged += (_, score) => _syncSessionContext.Score = score;
            MemoryScan.MemoryReadObject.PlayingTimeChanged += (_, playTime) => _syncSessionContext.BaseMemoryTime = playTime;
            MemoryScan.MemoryReadObject.BeatmapIdentifierChanged += (_, beatmap) => _syncSessionContext.Beatmap = beatmap;
            MemoryScan.MemoryReadObject.OsuStatusChanged += (pre, current) => _syncSessionContext.OsuStatus = current;
            MemoryScan.Start(50, 1);
        }
    }
}