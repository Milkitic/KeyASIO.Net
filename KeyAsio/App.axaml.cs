using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using KeyAsio.MemoryReading;
using KeyAsio.Services;
using KeyAsio.Shared;
using KeyAsio.Shared.Realtime.Services;
using KeyAsio.Shared.Services;
using KeyAsio.Shared.Utils;
using KeyAsio.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text;
using KeyAsio.Shared.Realtime;

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

            var realtimeController = Program.Host.Services.GetRequiredService<RealtimeController>();

            var mainWindow = Program.Host.Services.GetRequiredService<MainWindow>();
            desktop.MainWindow = mainWindow;


            desktop.Exit += Desktop_Exit;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void StartMemoryScan()
    {
        var appSettings = Program.Host.Services.GetRequiredService<AppSettings>();
        if (!appSettings.Realtime.RealtimeMode) return;

        var logger = Program.Host.Services.GetRequiredService<ILogger<App>>();
        var realtimeSessionContext = Program.Host.Services.GetRequiredService<RealtimeSessionContext>();
        var memoryScan = Program.Host.Services.GetRequiredService<MemoryScan>();

        try
        {
            var player = EncodeUtils.FromBase64String(appSettings.Logging.PlayerBase64 ?? "", Encoding.ASCII);
            realtimeSessionContext.Username = player;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to decode PlayerBase64 string.");
        }

        memoryScan.MemoryReadObject.PlayerNameChanged += (_, player) =>
            Dispatcher.UIThread.InvokeAsync(() => realtimeSessionContext.Username = player);
        memoryScan.MemoryReadObject.ModsChanged += (_, mods) =>
            Dispatcher.UIThread.InvokeAsync(() => realtimeSessionContext.PlayMods = mods);
        memoryScan.MemoryReadObject.ComboChanged += (_, combo) =>
            Dispatcher.UIThread.InvokeAsync(() => realtimeSessionContext.Combo = combo);
        memoryScan.MemoryReadObject.ScoreChanged += (_, score) =>
            Dispatcher.UIThread.InvokeAsync(() => realtimeSessionContext.Score = score);
        memoryScan.MemoryReadObject.IsReplayChanged += (_, isReplay) =>
            Dispatcher.UIThread.InvokeAsync(() => realtimeSessionContext.IsReplay = isReplay);
        memoryScan.MemoryReadObject.PlayingTimeChanged += (_, playTime) =>
            Dispatcher.UIThread.InvokeAsync(() => realtimeSessionContext.BaseMemoryTime = playTime);
        memoryScan.MemoryReadObject.BeatmapIdentifierChanged += (_, beatmap) =>
            Dispatcher.UIThread.InvokeAsync(() => realtimeSessionContext.Beatmap = beatmap);
        memoryScan.MemoryReadObject.OsuStatusChanged += (pre, current) =>
            Dispatcher.UIThread.InvokeAsync(() => realtimeSessionContext.OsuStatus = current);
        memoryScan.MemoryReadObject.ProcessIdChanged += (_, id) =>
            Dispatcher.UIThread.InvokeAsync(() => realtimeSessionContext.ProcessId = id);
        memoryScan.Start(appSettings.Realtime.Scanning.GeneralInterval,
            appSettings.Realtime.Scanning.TimingInterval);
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