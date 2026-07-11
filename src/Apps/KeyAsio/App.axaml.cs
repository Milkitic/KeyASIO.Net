using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using KeyAsio.Core.Audio;
using KeyAsio.Plugins.Contracts;
using KeyAsio.Services;
using KeyAsio.Configuration;
using KeyAsio.Application.Localization;
using KeyAsio.Sync.Sources;
using KeyAsio.Application.Plugins;
using KeyAsio.Application.Services;
using KeyAsio.Sync;
using KeyAsio.Views;
using Microsoft.Extensions.DependencyInjection;

namespace KeyAsio;

public partial class App : Avalonia.Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        //I18NExtension.Culture = new CultureInfo("en-US");
        LocalizationService.Instance.ConfigureStringResolver(static key => KeyAsio.Lang.SR.ResourceManager.GetString(key) ?? key);
        LocalizationService.Instance.ConfigureCultureApplier(static culture => KeyAsio.Lang.SR.Culture = culture);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Ensure SettingsManager is initialized to subscribe to settings changes
            var services = Program.Host.Services;
            services.GetRequiredService<SettingsManager>();

            var memorySyncBridge = services.GetRequiredService<MemorySyncBridge>();
            memorySyncBridge.Start();

            _ = services.GetRequiredService<RtssMonitorService>();

            var keyboardBindingInitializer = services.GetRequiredService<KeyboardBindingInitializer>();
            keyboardBindingInitializer.Setup();
            var appSettings = services.GetRequiredService<AppSettings>();
            keyboardBindingInitializer.RegisterAllKeys();

            var pluginManager = services.GetRequiredService<IPluginManager>();

            // InternalPlugins
            pluginManager.LoadPlugins(AppDomain.CurrentDomain.BaseDirectory, "KeyAsio.Plugins.*.dll",
                SearchOption.TopDirectoryOnly);

            // // Load external plugins
            // var pluginDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");
            // pluginManager.LoadPlugins(pluginDir, "*.dll", SearchOption.AllDirectories);

            pluginManager.InitializePlugins();
            pluginManager.StartupPlugins();

            var syncController = services.GetRequiredService<SyncController>();
            syncController.Start();

            var presetManager = services.GetRequiredService<PresetManager>();
            presetManager.Initialize();

            var mainWindow = services.GetRequiredService<MainWindow>();
            desktop.MainWindow = mainWindow;
            desktop.Exit += Desktop_Exit;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private async void Desktop_Exit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        await Program.Host.StopAsync();
    }
}
