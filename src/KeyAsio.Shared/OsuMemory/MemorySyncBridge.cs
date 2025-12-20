using System.Text;
using KeyAsio.Shared.Sync;
using KeyAsio.Shared.Utils;
using Microsoft.Extensions.Logging;

namespace KeyAsio.Shared.OsuMemory;

public class MemorySyncBridge
{
    private readonly MemoryScan _memoryScan;
    private readonly SyncSessionContext _syncSessionContext;
    private readonly AppSettings _appSettings;
    private readonly ILogger<MemorySyncBridge> _logger;

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
            SafeUpdate(() => _syncSessionContext.Username = player, nameof(_syncSessionContext.Username));

        _memoryScan.MemoryReadObject.ModsChanged += (_, mods) =>
            SafeUpdate(() => _syncSessionContext.PlayMods = mods, nameof(_syncSessionContext.PlayMods));

        _memoryScan.MemoryReadObject.ComboChanged += (_, combo) =>
            SafeUpdate(() => _syncSessionContext.Combo = combo, nameof(_syncSessionContext.Combo));

        _memoryScan.MemoryReadObject.ScoreChanged += (_, score) =>
            SafeUpdate(() => _syncSessionContext.Score = score, nameof(_syncSessionContext.Score));

        _memoryScan.MemoryReadObject.IsReplayChanged += (_, isReplay) =>
            SafeUpdate(() => _syncSessionContext.IsReplay = isReplay, nameof(_syncSessionContext.IsReplay));

        _memoryScan.MemoryReadObject.BeatmapIdentifierChanged += (_, beatmap) =>
            SafeUpdate(() => _syncSessionContext.Beatmap = beatmap, nameof(_syncSessionContext.Beatmap));

        _memoryScan.MemoryReadObject.OsuStatusChanged += (pre, current) =>
            SafeUpdate(() => _syncSessionContext.OsuStatus = current, nameof(_syncSessionContext.OsuStatus));

        _memoryScan.MemoryReadObject.ProcessIdChanged += (_, id) =>
            SafeUpdate(() => _syncSessionContext.ProcessId = id, nameof(_syncSessionContext.ProcessId));

        _memoryScan.MemoryReadObject.PlayingTimeChanged += (_, playTime) =>
            SafeUpdate(() => _syncSessionContext.BaseMemoryTime = playTime, nameof(_syncSessionContext.BaseMemoryTime));
    }

    private void SafeUpdate(Action action, string propertyName)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update SyncSessionContext.{PropertyName}", propertyName);
        }
    }
}