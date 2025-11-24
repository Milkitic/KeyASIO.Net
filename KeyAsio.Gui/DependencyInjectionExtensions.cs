using KeyAsio.Gui.Services;
using KeyAsio.Gui.Windows;
using KeyAsio.Shared.Models;
using Microsoft.Extensions.DependencyInjection;

namespace KeyAsio.Gui;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddGuiModule(this IServiceCollection services)
    {
        services.AddSingleton<SharedViewModel>();

        services.AddTransient<DeviceWindowViewModel>();
        services.AddTransient<MainWindow>();
        services.AddTransient<DeviceWindow>();
        services.AddTransient<LatencyGuideWindow>();
        services.AddTransient<RealtimeOptionsWindow>();

        services.AddSingleton<KeyboardBindingInitializer>();

        services.AddHostedService<StartupService>();
        return services;
    }
}