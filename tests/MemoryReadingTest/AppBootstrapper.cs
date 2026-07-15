using KeyAsio.Configuration;
using KeyAsio.Application.Models;
using KeyAsio.Sync.Sources;
using KeyAsio.Sync;
using KeyAsio.Sync.Services;
using KeyAsio.Plugins.Contracts;
using KeyAsio.Application.Plugins;
using KeyAsio.Sync.Abstractions;
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
            services.AddSingleton<ApplicationState>();
            services.AddSingleton<GameplayAudioService>();
            services.AddSingleton<MemoryScan>();
            services.AddSingleton<BeatmapHitsoundLoader>();
            services.AddSingleton<SfxPlaybackService>();
            services.AddSingleton<IPluginManager, PluginManager>();
            services.AddSingleton<ISyncExtensionHost, SyncPluginCoordinator>();
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
