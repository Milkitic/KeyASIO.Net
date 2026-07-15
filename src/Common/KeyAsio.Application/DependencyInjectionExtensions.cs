using KeyAsio.Application.Plugins;
using KeyAsio.Plugins.Contracts;
using KeyAsio.Sync.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace KeyAsio.Application;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddApplicationModule(this IServiceCollection services)
    {
        services.AddSingleton<AudioEngineWrapper>();
        services.AddSingleton<IAudioEngine>(static provider =>
            provider.GetRequiredService<AudioEngineWrapper>());

        services.AddSingleton<PluginSettingsAdapter>();
        services.AddSingleton<IPluginSettings>(static provider =>
            provider.GetRequiredService<PluginSettingsAdapter>());

        services.AddSingleton<GameplaySessionAdapter>();
        services.AddSingleton<IGameplaySession>(static provider =>
            provider.GetRequiredService<GameplaySessionAdapter>());

        services.AddSingleton<IPluginManager, PluginManager>();
        services.AddSingleton<ISyncExtensionHost, SyncPluginCoordinator>();
        return services;
    }
}
