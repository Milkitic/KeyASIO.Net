using System;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using KeyAsio.Gui.Utils;
using KeyAsio.Gui.Windows;
using KeyAsio.MemoryReading;
using KeyAsio.Shared;
using KeyAsio.Shared.Realtime;
using KeyAsio.Shared.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KeyAsio.Gui;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private readonly ILogger<App> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly YamlAppSettings _appSettings;
    private readonly MemoryScan _memoryScan;
    private readonly RealtimeSessionContext _realtimeSessionContext;

    public App(ILogger<App> logger,
        IServiceProvider serviceProvider,
        YamlAppSettings appSettings,
        MemoryScan memoryScan,
        RealtimeSessionContext realtimeSessionContext)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _appSettings = appSettings;
        _memoryScan = memoryScan;
        _realtimeSessionContext = realtimeSessionContext;
    }

    private void App_OnStartup(object sender, StartupEventArgs e)
    {
        StartMemoryScan();

        NLogDevice.RegisterDefault(_serviceProvider.GetRequiredService<ILogger<NLogDevice>>());

        UiDispatcher.SetUiSynchronizationContext(new DispatcherSynchronizationContext());
        Dispatcher.UnhandledException += Dispatcher_UnhandledException;

        MainWindow = Program.Host.Services.GetRequiredService<MainWindow>();
        MainWindow.Show();
    }

    private void StartMemoryScan()
    {
        if (!_appSettings.Realtime.RealtimeMode) return;

        try
        {
            var player = EncodeUtils.FromBase64String(_appSettings.Logging.PlayerBase64 ?? "", Encoding.ASCII);
            _realtimeSessionContext.Username = player;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to decode PlayerBase64 string.");
        }

        var dispatcher = Current.Dispatcher;
        _memoryScan.MemoryReadObject.PlayerNameChanged += (_, player) =>
            dispatcher.InvokeAsync(() => _realtimeSessionContext.Username = player);
        _memoryScan.MemoryReadObject.ModsChanged += (_, mods) =>
            dispatcher.InvokeAsync(() => _realtimeSessionContext.PlayMods = mods);
        _memoryScan.MemoryReadObject.ComboChanged += (_, combo) =>
            dispatcher.InvokeAsync(() => _realtimeSessionContext.Combo = combo);
        _memoryScan.MemoryReadObject.ScoreChanged += (_, score) =>
            dispatcher.InvokeAsync(() => _realtimeSessionContext.Score = score);
        _memoryScan.MemoryReadObject.IsReplayChanged += (_, isReplay) =>
            dispatcher.InvokeAsync(() => _realtimeSessionContext.IsReplay = isReplay);
        _memoryScan.MemoryReadObject.PlayingTimeChanged += (_, playTime) =>
            dispatcher.InvokeAsync(() => _realtimeSessionContext.BaseMemoryTime = playTime);
        _memoryScan.MemoryReadObject.BeatmapIdentifierChanged += (_, beatmap) =>
            dispatcher.InvokeAsync(() => _realtimeSessionContext.Beatmap = beatmap);
        _memoryScan.MemoryReadObject.OsuStatusChanged += (pre, current) =>
            dispatcher.InvokeAsync(() => _realtimeSessionContext.OsuStatus = current);
        _memoryScan.Start(_appSettings.Realtime.Scanning.GeneralInterval,
            _appSettings.Realtime.Scanning.TimingInterval);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Dispatcher_UnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _logger.LogError(e.Exception, "Unhandled Exception (Dispatcher)");
        e.Handled = true;
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await Program.Host.StopAsync();
        base.OnExit(e);
    }
}
