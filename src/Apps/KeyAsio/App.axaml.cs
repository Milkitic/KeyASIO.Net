using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using KeyAsio.Core.Audio;
using KeyAsio.Plugins.Abstractions;
using KeyAsio.Services;
using KeyAsio.Shared;
using KeyAsio.Shared.OsuMemory;
using KeyAsio.Shared.Plugins;
using KeyAsio.Shared.Services;
using KeyAsio.Shared.Sync;
using KeyAsio.Views;
using Microsoft.Extensions.DependencyInjection;

namespace KeyAsio;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        //I18NExtension.Culture = new CultureInfo("en-US");
        UiDispatcher.SetUiSynchronizationContext();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Ensure SettingsManager is initialized to subscribe to settings changes
            Program.Host.Services.GetRequiredService<SettingsManager>();

            var memorySyncBridge = Program.Host.Services.GetRequiredService<MemorySyncBridge>();
            memorySyncBridge.Start();

            _ = Program.Host.Services.GetRequiredService<RtssMonitorService>();

            var keyboardBindingInitializer = Program.Host.Services.GetRequiredService<KeyboardBindingInitializer>();
            keyboardBindingInitializer.Setup();
            var appSettings = Program.Host.Services.GetRequiredService<AppSettings>();
            keyboardBindingInitializer.RegisterAllKeys();

            var pluginManager = Program.Host.Services.GetRequiredService<IPluginManager>();
            var playbackEngine = Program.Host.Services.GetRequiredService<IPlaybackEngine>();

            // InternalPlugins
            pluginManager.LoadPlugins(AppDomain.CurrentDomain.BaseDirectory, "KeyAsio.Plugins.*.dll",
                SearchOption.TopDirectoryOnly);

            // // Load external plugins
            // var pluginDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");
            // pluginManager.LoadPlugins(pluginDir, "*.dll", SearchOption.AllDirectories);

            pluginManager.InitializePlugins(new AudioEngineWrapper(playbackEngine));
            pluginManager.StartupPlugins();

            var syncController = Program.Host.Services.GetRequiredService<SyncController>();
            syncController.Start();

            var presetManager = Program.Host.Services.GetRequiredService<PresetManager>();
            presetManager.Initialize();

            var mainWindow = Program.Host.Services.GetRequiredService<MainWindow>();
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