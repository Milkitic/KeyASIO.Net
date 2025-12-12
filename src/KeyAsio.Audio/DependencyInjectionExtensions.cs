using KeyAsio.Audio.Caching;
using Microsoft.Extensions.DependencyInjection;

namespace KeyAsio.Audio;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddAudioModule(this IServiceCollection services)
    {
        services.AddSingleton<AudioCacheManager>();

        services.AddSingleton<AudioEngine>();
        services.AddSingleton<AudioDeviceManager>();
        return services;
    }
}