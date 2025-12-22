using KeyAsio.Plugins.Abstractions;
using KeyAsio.Shared.Plugins;
using KeyAsio.Shared.Services;
using KeyAsio.Shared.Sync.Services;
using Microsoft.Extensions.DependencyInjection;

namespace KeyAsio.Shared.Sync;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddSyncModule(this IServiceCollection services)
    {
        services.AddSingleton<IPluginManager, PluginManager>();

        services.AddSingleton<GameplayAudioService>();
        services.AddSingleton<BeatmapHitsoundLoader>();
        services.AddSingleton<SfxPlaybackService>();
        services.AddSingleton<GameplaySessionManager>();

        services.AddSingleton<SyncSessionContext>();
        services.AddSingleton<SyncController>();

        services.AddSingleton<SkinManager>();
        return services;
    }
}