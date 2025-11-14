using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using KeyAsio.Gui.Utils;
using KeyAsio.MemoryReading;
using KeyAsio.Shared;
using KeyAsio.Shared.Realtime;
using KeyAsio.Shared.Utils;
using Microsoft.Extensions.Hosting;

namespace KeyAsio.Gui;

internal class StartupService : IHostedService
{
    private readonly AppSettings _appSettings;
    private readonly RealtimeModeManager _realtime;

    public StartupService(AppSettings appSettings, RealtimeModeManager realtime)
    {
        _appSettings = appSettings;
        _realtime = realtime;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_appSettings.Debugging)
        {
            ConsoleManager.Show();
        }

        StartMemoryScan();
    }

    private void StartMemoryScan()
    {
        if (!_appSettings.RealtimeOptions.RealtimeMode) return;

        try
        {
            var player = EncodeUtils.FromBase64String(_appSettings.PlayerBase64, Encoding.ASCII);
            _realtime.Username = player;
        }
        catch
        {
            // ignored
        }

        var dispatcher = Application.Current.Dispatcher;
        KeyAsio.MemoryReading.Logger.SetLoggerFactory(LogUtils.LoggerFactory);
        MemoryScan.MemoryReadObject.PlayerNameChanged += (_, player) =>
            dispatcher.InvokeAsync(() => _realtime.Username = player);
        MemoryScan.MemoryReadObject.ModsChanged += (_, mods) =>
            dispatcher.InvokeAsync(() => _realtime.PlayMods = mods);
        MemoryScan.MemoryReadObject.ComboChanged += (_, combo) =>
            dispatcher.InvokeAsync(() => _realtime.Combo = combo);
        MemoryScan.MemoryReadObject.ScoreChanged += (_, score) =>
            dispatcher.InvokeAsync(() => _realtime.Score = score);
        MemoryScan.MemoryReadObject.IsReplayChanged += (_, isReplay) =>
            dispatcher.InvokeAsync(() => _realtime.IsReplay = isReplay);
        MemoryScan.MemoryReadObject.PlayingTimeChanged += (_, playTime) =>
            dispatcher.InvokeAsync(() => _realtime.LastFetchedPlayTime = playTime);
        MemoryScan.MemoryReadObject.BeatmapIdentifierChanged += (_, beatmap) =>
            dispatcher.InvokeAsync(() => _realtime.Beatmap = beatmap);
        MemoryScan.MemoryReadObject.OsuStatusChanged += (pre, current) =>
            dispatcher.InvokeAsync(() => _realtime.OsuStatus = current);
        MemoryScan.Start(_appSettings.RealtimeOptions.GeneralScanInterval, _appSettings.RealtimeOptions.TimingScanInterval);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
