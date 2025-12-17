using System.Text;
using KeyAsio.Shared.Sync;
using KeyAsio.Shared.Utils;
using Microsoft.Extensions.Logging;
using Milki.Extensions.Threading;

namespace KeyAsio.Shared.OsuMemory;

public class MemorySyncBridge
{
    private readonly MemoryScan _memoryScan;
    private readonly SyncSessionContext _syncSessionContext;
    private readonly AppSettings _appSettings;
    private readonly ILogger<MemorySyncBridge> _logger;
    private readonly SingleSynchronizationContext _singleSynchronizationContext = new("Memory events callback thread");

    public MemorySyncBridge(
        MemoryScan memoryScan,
        SyncSessionContext syncSessionContext,
        AppSettings appSettings,
        ILogger<MemorySyncBridge> logger)
    {
        _memoryScan = memoryScan;
        _syncSessionContext = syncSessionContext;
        _appSettings = appSettings;
        _logger = logger;
    }

    public void Start()
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

        BindEvents();

        _appSettings.Sync.Scanning.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(AppSettingsSyncScanning.GeneralScanInterval)
                or nameof(AppSettingsSyncScanning.TimingScanInterval))
            {
                _memoryScan.UpdateIntervals(_appSettings.Sync.Scanning.GeneralScanInterval,
                    _appSettings.Sync.Scanning.TimingScanInterval);
            }
        };

        _memoryScan.Start(_appSettings.Sync.Scanning.GeneralScanInterval,
            _appSettings.Sync.Scanning.TimingScanInterval);
    }

    private void BindEvents()
    {
        _memoryScan.MemoryReadObject.PlayerNameChanged += (_, player) =>
            _singleSynchronizationContext.Post(_ => _syncSessionContext.Username = player, null);
        _memoryScan.MemoryReadObject.ModsChanged += (_, mods) =>
            _singleSynchronizationContext.Post(_ => _syncSessionContext.PlayMods = mods, null);
        _memoryScan.MemoryReadObject.ComboChanged += (_, combo) =>
            _singleSynchronizationContext.Post(_ => _syncSessionContext.Combo = combo, null);
        _memoryScan.MemoryReadObject.ScoreChanged += (_, score) =>
            _singleSynchronizationContext.Post(_ => _syncSessionContext.Score = score, null);
        _memoryScan.MemoryReadObject.IsReplayChanged += (_, isReplay) =>
            _singleSynchronizationContext.Post(_ => _syncSessionContext.IsReplay = isReplay, null);
        _memoryScan.MemoryReadObject.PlayingTimeChanged += (_, playTime) =>
            _singleSynchronizationContext.Post(_ => _syncSessionContext.BaseMemoryTime = playTime, null);
        _memoryScan.MemoryReadObject.BeatmapIdentifierChanged += (_, beatmap) =>
            _singleSynchronizationContext.Post(_ => _syncSessionContext.Beatmap = beatmap, null);
        _memoryScan.MemoryReadObject.OsuStatusChanged += (pre, current) =>
            _singleSynchronizationContext.Post(_ => _syncSessionContext.OsuStatus = current, null);
        _memoryScan.MemoryReadObject.ProcessIdChanged += (_, id) =>
            _singleSynchronizationContext.Post(_ => _syncSessionContext.ProcessId = id, null);
    }
}