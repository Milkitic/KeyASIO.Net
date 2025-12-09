using KeyAsio.Shared.Realtime.Services;
using KeyAsio.Shared.Services;
using Microsoft.Extensions.DependencyInjection;

namespace KeyAsio.Shared.Realtime;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddRealtimeModule(this IServiceCollection services)
    {
        services.AddSingleton<GameplayAudioService>();
        services.AddSingleton<BeatmapHitsoundLoader>();
        services.AddSingleton<BackgroundMusicManager>();
        services.AddSingleton<SfxPlaybackService>();
        services.AddSingleton<GameplaySessionManager>();

        services.AddSingleton<RealtimeSessionContext>();
        services.AddSingleton<RealtimeController>();

        services.AddSingleton<SkinManager>();
        return services;
    }
}