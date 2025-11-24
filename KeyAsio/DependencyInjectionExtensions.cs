using KeyAsio.Services;
using KeyAsio.Shared.Models;
using KeyAsio.Views;
using Microsoft.Extensions.DependencyInjection;

namespace KeyAsio;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddGuiModule(this IServiceCollection services)
    {
        services.AddSingleton<SharedViewModel>();

        services.AddTransient<MainWindow>();

        services.AddHostedService<GuiStartupService>();
        return services;
    }
}