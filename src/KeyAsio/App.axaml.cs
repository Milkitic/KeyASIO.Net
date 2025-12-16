using System.Text;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using KeyAsio.Services;
using KeyAsio.Shared;
using KeyAsio.Shared.OsuMemory;
using KeyAsio.Shared.Services;
using KeyAsio.Shared.Sync;
using KeyAsio.Shared.Utils;
using KeyAsio.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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

            StartMemoryScan();

            var keyboardBindingInitializer = Program.Host.Services.GetRequiredService<KeyboardBindingInitializer>();
            keyboardBindingInitializer.Setup();
            var appSettings = Program.Host.Services.GetRequiredService<AppSettings>();
            keyboardBindingInitializer.RegisterKeys(appSettings.Input.Keys);
            //_ = keyboardBindingInitializer.InitializeKeyAudioAsync();

            var syncController = Program.Host.Services.GetRequiredService<SyncController>();

            var mainWindow = Program.Host.Services.GetRequiredService<MainWindow>();
            desktop.MainWindow = mainWindow;


            desktop.Exit += Desktop_Exit;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void StartMemoryScan()
    {
        var appSettings = Program.Host.Services.GetRequiredService<AppSettings>();
        if (!appSettings.Sync.EnableSync) return;

        var logger = Program.Host.Services.GetRequiredService<ILogger<App>>();
        var syncSessionContext = Program.Host.Services.GetRequiredService<SyncSessionContext>();
        var memoryScan = Program.Host.Services.GetRequiredService<MemoryScan>();

        try
        {
            var player = EncodeUtils.FromBase64String(appSettings.Logging.PlayerBase64 ?? "", Encoding.ASCII);
            syncSessionContext.Username = player;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to decode PlayerBase64 string.");
        }

        memoryScan.MemoryReadObject.PlayerNameChanged += (_, player) =>
            syncSessionContext.Username = player;
        memoryScan.MemoryReadObject.ModsChanged += (_, mods) =>
            syncSessionContext.PlayMods = mods;
        memoryScan.MemoryReadObject.ComboChanged += (_, combo) =>
            syncSessionContext.Combo = combo;
        memoryScan.MemoryReadObject.ScoreChanged += (_, score) =>
            syncSessionContext.Score = score;
        memoryScan.MemoryReadObject.IsReplayChanged += (_, isReplay) =>
            syncSessionContext.IsReplay = isReplay;
        memoryScan.MemoryReadObject.PlayingTimeChanged += (_, playTime) =>
            syncSessionContext.BaseMemoryTime = playTime;
        memoryScan.MemoryReadObject.BeatmapIdentifierChanged += (_, beatmap) =>
            syncSessionContext.Beatmap = beatmap;
        memoryScan.MemoryReadObject.OsuStatusChanged += (pre, current) =>
            syncSessionContext.OsuStatus = current;
        memoryScan.MemoryReadObject.ProcessIdChanged += (_, id) =>
            syncSessionContext.ProcessId = id;

        appSettings.Sync.Scanning.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(AppSettingsSyncScanning.GeneralScanInterval)
                or nameof(AppSettingsSyncScanning.TimingScanInterval))
            {
                memoryScan.UpdateIntervals(appSettings.Sync.Scanning.GeneralScanInterval,
                    appSettings.Sync.Scanning.TimingScanInterval);
            }
        };

        memoryScan.Start(appSettings.Sync.Scanning.GeneralScanInterval,
            appSettings.Sync.Scanning.TimingScanInterval);
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