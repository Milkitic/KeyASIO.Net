using KeyAsio.Shared;
using KeyAsio.Shared.Models;
using KeyAsio.Shared.OsuMemory;
using KeyAsio.Shared.Sync;
using KeyAsio.Shared.Sync.Services;
using KeyAsio.Plugins.Abstractions;
using KeyAsio.Shared.Plugins;
using Microsoft.Extensions.DependencyInjection;
using NLog.Extensions.Logging;
using System;

namespace MemoryReadingTest
{
    public static class AppBootstrapper
    {
        public static IServiceProvider InitServices()
        {
            var services = new ServiceCollection();
            services.AddNLog();
            services.AddSingleton(new AppSettings());
            services.AddSingleton<SharedViewModel>();
            services.AddSingleton<GameplayAudioService>();
            services.AddSingleton<MemoryScan>();
            services.AddSingleton<BeatmapHitsoundLoader>();
            services.AddSingleton<SfxPlaybackService>();
            services.AddSingleton<IPluginManager, PluginManager>();
            services.AddSingleton<SyncController>();
            services.AddSingleton<SyncSessionContext>();
            return services.BuildServiceProvider();
        }

        public static void ConfigureMemoryScan(IServiceProvider provider)
        {
            var syncSessionContext = provider.GetRequiredService<SyncSessionContext>();
            var memoryScan = provider.GetRequiredService<MemoryScan>();

            memoryScan.MemoryReadObject.PlayerNameChanged += (_, player) => syncSessionContext.Username = player;
            memoryScan.MemoryReadObject.ModsChanged += (_, mods) => syncSessionContext.PlayMods = mods;
            memoryScan.MemoryReadObject.ComboChanged += (_, combo) => syncSessionContext.Combo = combo;
            memoryScan.MemoryReadObject.ScoreChanged += (_, score) => syncSessionContext.Score = score;
            memoryScan.MemoryReadObject.PlayingTimeChanged += (_, playTime) => syncSessionContext.BaseMemoryTime = playTime;
            memoryScan.MemoryReadObject.BeatmapIdentifierChanged += (_, beatmap) => syncSessionContext.Beatmap = beatmap;
            memoryScan.MemoryReadObject.OsuStatusChanged += (pre, current) => syncSessionContext.OsuStatus = current;
            
            memoryScan.Start(50, 1);
        }
    }
}
