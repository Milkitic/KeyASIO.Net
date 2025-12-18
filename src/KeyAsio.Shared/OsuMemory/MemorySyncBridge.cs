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
            _syncSessionContext.Username = player;
        _memoryScan.MemoryReadObject.ModsChanged += (_, mods) =>
            _syncSessionContext.PlayMods = mods;
        _memoryScan.MemoryReadObject.ComboChanged += (_, combo) =>
            _syncSessionContext.Combo = combo;
        _memoryScan.MemoryReadObject.ScoreChanged += (_, score) =>
            _syncSessionContext.Score = score;
        _memoryScan.MemoryReadObject.IsReplayChanged += (_, isReplay) =>
            _syncSessionContext.IsReplay = isReplay;
        _memoryScan.MemoryReadObject.PlayingTimeChanged += (_, playTime) =>
            _syncSessionContext.BaseMemoryTime = playTime;
        _memoryScan.MemoryReadObject.BeatmapIdentifierChanged += (_, beatmap) =>
            _syncSessionContext.Beatmap = beatmap;
        _memoryScan.MemoryReadObject.OsuStatusChanged += (pre, current) =>
            _syncSessionContext.OsuStatus = current;
        _memoryScan.MemoryReadObject.ProcessIdChanged += (_, id) =>
            _syncSessionContext.ProcessId = id;
    }
}