using KeyAsio.Core.OsuAudio.Hitsounds;
using KeyAsio.Plugins.Abstractions;
using KeyAsio.Plugins.Abstractions.OsuMemory;
using KeyAsio.Shared.Sync;

namespace KeyAsio.Shared.OsuMemory;

public sealed class LazerIpcGameSyncSource : IGameSyncSource
{
    private readonly LazerIpcBridge _lazerIpcBridge;
    private readonly GameSyncSnapshot _snapshot;
    private readonly LazerIpcFrame _frame = new();
    private bool _eventsBound;
    private bool _connected;
    private IBeatmapResourceCatalog? _resourceCatalog;

    public LazerIpcGameSyncSource(LazerIpcBridge lazerIpcBridge)
    {
        _lazerIpcBridge = lazerIpcBridge;
        _snapshot = GameSyncSnapshot.NotRunning(ClientType);
        CurrentSnapshot = _snapshot;
    }

    public string Name => "osu!lazer IPC";
    public GameClientType ClientType => GameClientType.Lazer;
    public int Priority => 100;
    public bool IsAvailable => _connected;
    public GameSyncSnapshot CurrentSnapshot { get; private set; }

    public event Action<IGameSyncSource, bool>? AvailabilityChanged;
    public event Action<IGameSyncSource, GameSyncSnapshot>? SnapshotReceived;

    public void Start()
    {
        BindEvents();
        _lazerIpcBridge.Start();
    }

    public async Task StopAsync()
    {
        await _lazerIpcBridge.StopAsync();
        _connected = false;
        _resourceCatalog = null;
        _frame.Reset();
        _snapshot.ResetToNotRunning(ClientType);
        AvailabilityChanged?.Invoke(this, false);
    }

    private void BindEvents()
    {
        if (_eventsBound) return;

        _lazerIpcBridge.ConnectionChanged += OnConnectionChanged;
        _lazerIpcBridge.FrameReceived += OnFrameReceived;
        _eventsBound = true;
    }

    private void OnConnectionChanged(bool oldValue, bool newValue)
    {
        _connected = newValue;
        if (!newValue)
        {
            _resourceCatalog = null;
            _frame.Reset();
            _snapshot.ResetToNotRunning(ClientType);
        }

        AvailabilityChanged?.Invoke(this, newValue);
    }

    private void OnFrameReceived(LazerIpcDeltaFrame deltaFrame)
    {
        _connected = true;
        var beatmapChanged = deltaFrame.HasField(LazerIpcFieldKind.BeatmapFolder) ||
                             deltaFrame.HasField(LazerIpcFieldKind.BeatmapFilename);
        var beatmapFilesChanged = deltaFrame.HasField(LazerIpcFieldKind.BeatmapFiles);

        if (beatmapChanged && !beatmapFilesChanged)
        {
            _resourceCatalog = null;
            _frame.ClearBeatmapFiles();
        }

        _frame.Apply(deltaFrame);
        var frame = _frame;

        var status = Enum.IsDefined(typeof(OsuMemoryStatus), frame.Status)
            ? (OsuMemoryStatus)frame.Status
            : OsuMemoryStatus.Unknown;

        if (beatmapFilesChanged && frame.BeatmapFiles.Length > 0)
        {
            var resourceCatalog = BeatmapResourceCatalog.FromMappings(
                frame.BeatmapFiles.Select(file => new BeatmapResource(file.Name, file.Path)),
                frame.BeatmapFolder,
                CreateCatalogCacheKey(frame));

            if (!resourceCatalog.IsEmpty)
            {
                _resourceCatalog = resourceCatalog;
            }
        }

        var beatmap = !string.IsNullOrWhiteSpace(frame.BeatmapFolder) &&
                      !string.IsNullOrWhiteSpace(frame.BeatmapFilename)
            ? new BeatmapIdentifier(frame.BeatmapFolder, frame.BeatmapFilename)
            : default;

        var snapshot = _snapshot;
        snapshot.ProcessId = frame.ProcessId;
        snapshot.Username = frame.Username;
        snapshot.PlayMods = (Mods)frame.Mods;
        snapshot.IsReplay = frame.IsReplay;
        snapshot.Score = frame.Score;
        snapshot.Combo = frame.Combo;
        snapshot.Statistics = frame.Statistics;
        snapshot.HitErrors = new SyncHitErrors(frame.HitErrorIndex, frame.HitErrors);
        snapshot.Beatmap = beatmap;
        snapshot.BeatmapResourceCatalog = _resourceCatalog;
        snapshot.PlayTime = frame.PlayTime;
        snapshot.Status = status;

        SnapshotReceived?.Invoke(this, _snapshot);
    }

    private static string CreateCatalogCacheKey(LazerIpcFrame frame)
    {
        var beatmapFilename = frame.BeatmapFilename;
        var beatmapPath = string.IsNullOrWhiteSpace(beatmapFilename)
            ? null
            : frame.BeatmapFiles.FirstOrDefault(file =>
                string.Equals(BeatmapResourceCatalog.NormalizeName(file.Name),
                    BeatmapResourceCatalog.NormalizeName(beatmapFilename), StringComparison.OrdinalIgnoreCase))?.Path;

        return $"lazer:{frame.BeatmapFolder}:{beatmapFilename}:{beatmapPath}:{frame.BeatmapFiles.Length}";
    }
}
