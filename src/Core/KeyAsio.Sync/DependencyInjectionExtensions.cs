using KeyAsio.Sync.Abstractions;
using KeyAsio.Sync.Services;
using KeyAsio.Sync.Sources;
using Microsoft.Extensions.DependencyInjection;

namespace KeyAsio.Sync;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddSyncModule(this IServiceCollection services)
    {
        services.AddSingleton<MemoryScan>();
        services.AddSingleton<MemorySyncBridge>();
        services.AddSingleton<LazerIpcBridge>();
        services.AddSingleton<StableMemoryGameSyncSource>();
        services.AddSingleton<LazerIpcGameSyncSource>();
        services.AddSingleton<IGameSyncSource>(serviceProvider =>
            serviceProvider.GetRequiredService<StableMemoryGameSyncSource>());
        services.AddSingleton<IGameSyncSource>(serviceProvider =>
            serviceProvider.GetRequiredService<LazerIpcGameSyncSource>());
        services.AddSingleton<GameSyncSourceCoordinator>();
        services.AddSingleton<GameplayAudioService>();
        services.AddSingleton<IGameplayAudioCache>(static provider =>
            provider.GetRequiredService<GameplayAudioService>());
        services.AddSingleton<BeatmapHitsoundLoader>();
        services.AddSingleton<SfxPlaybackService>();
        services.AddSingleton<GameplaySessionManager>();

        services.AddSingleton<SyncSessionContext>();
        services.AddSingleton<SyncController>();

        return services;
    }
}
