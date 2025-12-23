using KeyAsio.Services;
using KeyAsio.Shared.Models;
using KeyAsio.ViewModels;
using KeyAsio.Views;
using Microsoft.Extensions.DependencyInjection;
using SukiUI.Dialogs;
using SukiUI.Toasts;

namespace KeyAsio;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddGuiModule(this IServiceCollection services)
    {
        services.AddSingleton<SharedViewModel>();

        services.AddSingleton<ISukiDialogManager, SukiDialogManager>();
        services.AddSingleton<ISukiToastManager, SafeSukiToastManager>();

        services.AddTransient<MainWindow>();
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<AudioSettingsViewModel>();
        services.AddTransient<KeyBindingViewModel>();
        services.AddTransient<PluginManagerViewModel>();

        services.AddSingleton<KeyboardBindingInitializer>();

        services.AddHostedService<GuiStartupService>();
        return services;
    }
}