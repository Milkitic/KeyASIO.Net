using System;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using KeyAsio.Audio.SampleProviders.BalancePans;
using KeyAsio.Audio.Utils;
using KeyAsio.Gui.Utils;
using KeyAsio.Gui.Windows;
using KeyAsio.Shared;
using KeyAsio.Shared.OsuMemory;
using KeyAsio.Shared.Services;
using KeyAsio.Shared.Sync;
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
    private readonly AppSettings _appSettings;
    private readonly MemoryScan _memoryScan;
    private readonly SyncSessionContext _syncSessionContext;
    private readonly SkinManager _skinManager;

    public App(ILogger<App> logger,
        IServiceProvider serviceProvider,
        AppSettings appSettings,
        MemoryScan memoryScan,
        SyncSessionContext syncSessionContext,
        SkinManager skinManager)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _appSettings = appSettings;
        _memoryScan = memoryScan;
        _syncSessionContext = syncSessionContext;
        _skinManager = skinManager;
    }

    private void App_OnStartup(object sender, StartupEventArgs e)
    {
        SimdAudioConverter.EnableAvx512 = _appSettings.Performance.EnableAvx512;
        ProfessionalBalanceProvider.EnableAvx512 = _appSettings.Performance.EnableAvx512;

        if (_appSettings.Paths.AllowAutoLoadSkins == null)
        {
            _appSettings.Paths.AllowAutoLoadSkins = true;
        }

        _skinManager.Start();
        StartMemoryScan();

        NLogDevice.RegisterDefault(_serviceProvider.GetRequiredService<ILogger<NLogDevice>>());

        UiDispatcher.SetUiSynchronizationContext(new DispatcherSynchronizationContext());
        Dispatcher.UnhandledException += Dispatcher_UnhandledException;

        MainWindow = Program.Host.Services.GetRequiredService<MainWindow>();
        MainWindow.Show();
    }

    private void StartMemoryScan()
    {
        if (!_appSettings.Sync.EnableSync) return;

        try
        {
            var player = EncodeUtils.FromBase64String(_appSettings.Logging.PlayerBase64 ?? "", Encoding.ASCII);
            _syncSessionContext.Username = player;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to decode PlayerBase64 string.");
        }

        var dispatcher = Current.Dispatcher;
        _memoryScan.MemoryReadObject.PlayerNameChanged += (_, player) =>
            dispatcher.InvokeAsync(() => _syncSessionContext.Username = player);
        _memoryScan.MemoryReadObject.ModsChanged += (_, mods) =>
            dispatcher.InvokeAsync(() => _syncSessionContext.PlayMods = mods);
        _memoryScan.MemoryReadObject.ComboChanged += (_, combo) =>
            dispatcher.InvokeAsync(() => _syncSessionContext.Combo = combo);
        _memoryScan.MemoryReadObject.ScoreChanged += (_, score) =>
            dispatcher.InvokeAsync(() => _syncSessionContext.Score = score);
        _memoryScan.MemoryReadObject.IsReplayChanged += (_, isReplay) =>
            dispatcher.InvokeAsync(() => _syncSessionContext.IsReplay = isReplay);
        _memoryScan.MemoryReadObject.PlayingTimeChanged += (_, playTime) =>
            dispatcher.InvokeAsync(() => _syncSessionContext.BaseMemoryTime = playTime);
        _memoryScan.MemoryReadObject.BeatmapIdentifierChanged += (_, beatmap) =>
            dispatcher.InvokeAsync(() => _syncSessionContext.Beatmap = beatmap);
        _memoryScan.MemoryReadObject.OsuStatusChanged += (pre, current) =>
            dispatcher.InvokeAsync(() => _syncSessionContext.OsuStatus = current);
        _memoryScan.Start(_appSettings.Sync.Scanning.GeneralScanInterval,
            _appSettings.Sync.Scanning.TimingScanInterval);
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