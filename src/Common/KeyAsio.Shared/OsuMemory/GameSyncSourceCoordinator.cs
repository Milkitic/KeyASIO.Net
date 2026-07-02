using KeyAsio.Plugins.Abstractions.OsuMemory;
using KeyAsio.Shared.Sync;
using Microsoft.Extensions.Logging;

namespace KeyAsio.Shared.OsuMemory;

public sealed class GameSyncSourceCoordinator
{
    private readonly SyncSessionContext _syncSessionContext;
    private readonly IGameSyncSource[] _sources;
    private readonly ILogger<GameSyncSourceCoordinator> _logger;
    private IGameSyncSource? _activeSource;
    private bool _started;
    private GameSyncSnapshot? _disconnectedSnapshot;

    public GameSyncSourceCoordinator(
        SyncSessionContext syncSessionContext,
        IEnumerable<IGameSyncSource> sources,
        ILogger<GameSyncSourceCoordinator> logger)
    {
        _syncSessionContext = syncSessionContext;
        _sources = sources.OrderByDescending(source => source.Priority).ToArray();
        _logger = logger;

        foreach (var source in _sources)
        {
            source.AvailabilityChanged += OnSourceAvailabilityChanged;
            source.SnapshotReceived += OnSourceSnapshotReceived;
        }
    }

    public void Start()
    {
        if (_started) return;
        _started = true;

        foreach (var source in _sources)
        {
            source.Start();
        }

        SelectActiveSource(applyCurrentSnapshot: true);
    }

    public async Task StopAsync()
    {
        if (!_started) return;
        _started = false;

        foreach (var source in _sources)
        {
            await source.StopAsync();
        }

        _activeSource = null;
        ApplyDisconnectedSnapshot(GameClientType.Stable);
    }

    private void OnSourceAvailabilityChanged(IGameSyncSource source, bool isAvailable)
    {
        if (!_started) return;

        if (!isAvailable && ReferenceEquals(_activeSource, source))
        {
            _logger.LogInformation("Game sync source unavailable: {Source}", source.Name);
        }
        else if (isAvailable)
        {
            _logger.LogInformation("Game sync source available: {Source}", source.Name);
        }

        SelectActiveSource(applyCurrentSnapshot: true);
    }

    private void OnSourceSnapshotReceived(IGameSyncSource source, GameSyncSnapshot snapshot)
    {
        if (!_started) return;

        SelectActiveSource(applyCurrentSnapshot: false);
        if (!ReferenceEquals(_activeSource, source))
        {
            return;
        }

        ApplySnapshot(snapshot);
    }

    private void SelectActiveSource(bool applyCurrentSnapshot)
    {
        var nextSource = _sources.FirstOrDefault(source => source.IsAvailable);
        if (ReferenceEquals(_activeSource, nextSource))
        {
            return;
        }

        var oldSource = _activeSource;
        _activeSource = nextSource;

        _logger.LogInformation("Game sync source switched: {OldSource} -> {NewSource}",
            oldSource?.Name ?? "none", nextSource?.Name ?? "none");

        if (nextSource != null)
        {
            if (applyCurrentSnapshot)
            {
                ApplySnapshot(nextSource.CurrentSnapshot);
            }
        }
        else
        {
            ApplyDisconnectedSnapshot(oldSource?.ClientType ?? GameClientType.Stable);
        }
    }

    private void ApplySnapshot(GameSyncSnapshot snapshot)
    {
        try
        {
            _syncSessionContext.ClientType = snapshot.ClientType;
            _syncSessionContext.ProcessId = snapshot.ProcessId;
            _syncSessionContext.Username = snapshot.Username;
            _syncSessionContext.PlayMods = snapshot.PlayMods;
            _syncSessionContext.IsReplay = snapshot.IsReplay;
            _syncSessionContext.Score = snapshot.Score;
            _syncSessionContext.Combo = snapshot.Combo;
            _syncSessionContext.Statistics = snapshot.Statistics;
            _syncSessionContext.HitErrors = snapshot.HitErrors;
            _syncSessionContext.BeatmapResourceCatalog = snapshot.BeatmapResourceCatalog;
            _syncSessionContext.Beatmap = snapshot.Beatmap;
            _syncSessionContext.BaseMemoryTime = snapshot.PlayTime;
            _syncSessionContext.OsuStatus = snapshot.Status;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to apply game sync snapshot from {ClientType}.", snapshot.ClientType);
        }
    }

    private void ApplyDisconnectedSnapshot(GameClientType disconnectedClientType)
    {
        var fallbackClientType = disconnectedClientType == GameClientType.Lazer
            ? GameClientType.Stable
            : disconnectedClientType;

        // Reuse a single disconnected snapshot to avoid allocating on the
        // low-frequency disconnect path. ApplySnapshot only reads the fields
        // synchronously, so mutating a shared instance is safe.
        _disconnectedSnapshot ??= GameSyncSnapshot.NotRunning(fallbackClientType);
        _disconnectedSnapshot.ResetToNotRunning(fallbackClientType);
        ApplySnapshot(_disconnectedSnapshot);
    }
}
