using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using KeyAsio.Core.Audio;
using KeyAsio.Plugins.Abstractions;
using KeyAsio.Services;
using KeyAsio.Shared;
using KeyAsio.Shared.OsuMemory;
using KeyAsio.Shared.Plugins;
using KeyAsio.Shared.Services;
using KeyAsio.Shared.Sync;
using KeyAsio.ViewModels;
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
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();

            var skinManager = Program.Host.Services.GetRequiredService<SkinManager>();
            skinManager.Start();

            // Ensure SettingsManager is initialized to subscribe to settings changes
            Program.Host.Services.GetRequiredService<SettingsManager>();

            var memorySyncBridge = Program.Host.Services.GetRequiredService<MemorySyncBridge>();
            memorySyncBridge.Start();

            var keyboardBindingInitializer = Program.Host.Services.GetRequiredService<KeyboardBindingInitializer>();
            keyboardBindingInitializer.Setup();
            var appSettings = Program.Host.Services.GetRequiredService<AppSettings>();
            keyboardBindingInitializer.RegisterAllKeys();

            var pluginManager = Program.Host.Services.GetRequiredService<IPluginManager>();
            var audioEngine = Program.Host.Services.GetRequiredService<AudioEngine>();

            // InternalPlugins
            pluginManager.LoadPlugins(AppDomain.CurrentDomain.BaseDirectory, "KeyAsio.Plugins.*.dll",
                SearchOption.TopDirectoryOnly);

            // // Load external plugins
            // var pluginDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");
            // pluginManager.LoadPlugins(pluginDir, "*.dll", SearchOption.AllDirectories);

            pluginManager.InitializePlugins(new AudioEngineWrapper(audioEngine));
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

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }

    private async void Desktop_Exit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        await Program.Host.StopAsync();
    }
}