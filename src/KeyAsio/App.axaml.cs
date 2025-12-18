using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using KeyAsio.Services;
using KeyAsio.Shared;
using KeyAsio.Shared.OsuMemory;
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

#if DEBUG
        this.AttachDeveloperTools();
#endif
    }

    public override void OnFrameworkInitializationCompleted()
    {
        UiDispatcher.SetUiSynchronizationContext();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();

            var skinManager = Program.Host.Services.GetRequiredService<SkinManager>();
            skinManager.Start();

            var memorySyncBridge = Program.Host.Services.GetRequiredService<MemorySyncBridge>();
            memorySyncBridge.Start();

            var keyboardBindingInitializer = Program.Host.Services.GetRequiredService<KeyboardBindingInitializer>();
            keyboardBindingInitializer.Setup();
            var appSettings = Program.Host.Services.GetRequiredService<AppSettings>();
            keyboardBindingInitializer.RegisterKeys(appSettings.Input.Keys);
            //_ = keyboardBindingInitializer.InitializeKeyAudioAsync();

            var syncController = Program.Host.Services.GetRequiredService<SyncController>();
            syncController.Start();

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