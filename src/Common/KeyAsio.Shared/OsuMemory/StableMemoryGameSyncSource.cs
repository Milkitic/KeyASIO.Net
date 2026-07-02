using KeyAsio.Shared.Sync;

namespace KeyAsio.Shared.OsuMemory;

public sealed class StableMemoryGameSyncSource : IGameSyncSource
{
    private readonly MemoryScan _memoryScan;
    private readonly GameSyncSnapshot _snapshot;
    private bool _eventsBound;
    private bool _started;
    private int _generalInterval;
    private int _timingInterval;

    public StableMemoryGameSyncSource(MemoryScan memoryScan)
    {
        _memoryScan = memoryScan;
        _snapshot = new GameSyncSnapshot { ClientType = ClientType };
        UpdateSnapshot();
        CurrentSnapshot = _snapshot;
    }

    public string Name => "osu!stable memory";
    public GameClientType ClientType => GameClientType.Stable;
    public int Priority => 0;
    public bool IsAvailable => _started;
    public GameSyncSnapshot CurrentSnapshot { get; private set; }

    public event Action<IGameSyncSource, bool>? AvailabilityChanged;
    public event Action<IGameSyncSource, GameSyncSnapshot>? SnapshotReceived;

    public void ConfigureIntervals(int generalInterval, int timingInterval)
    {
        _generalInterval = generalInterval;
        _timingInterval = timingInterval;

        if (_started)
        {
            _memoryScan.UpdateIntervals(generalInterval, timingInterval);
        }
    }

    public void Start()
    {
        if (_started) return;

        BindEvents();
        _started = true;
        _memoryScan.Start(_generalInterval, _timingInterval);
        PublishSnapshot();
        AvailabilityChanged?.Invoke(this, true);
    }

    public async Task StopAsync()
    {
        if (!_started) return;

        await _memoryScan.StopAsync();
        _started = false;
        _snapshot.ResetToNotRunning(ClientType);
        AvailabilityChanged?.Invoke(this, false);
    }

    private void BindEvents()
    {
        if (_eventsBound) return;

        var memory = _memoryScan.MemoryReadObject;
        memory.PlayerNameChanged += (_, _) => PublishSnapshot();
        memory.ModsChanged += (_, _) => PublishSnapshot();
        memory.ComboChanged += (_, _) => PublishSnapshot();
        memory.ScoreChanged += (_, _) => PublishSnapshot();
        memory.IsReplayChanged += (_, _) => PublishSnapshot();
        memory.BeatmapIdentifierChanged += (_, _) => PublishSnapshot();
        memory.OsuStatusChanged += (_, _) => PublishSnapshot();
        memory.ProcessIdChanged += (_, _) => PublishSnapshot();
        memory.PlayingTimeChanged += (_, _) => PublishSnapshot();
        memory.StatisticsChanged += (_, _) => PublishSnapshot();
        memory.HitErrorsChanged += (_, _) => PublishSnapshot();

        _eventsBound = true;
    }

    private void PublishSnapshot()
    {
        UpdateSnapshot();
        SnapshotReceived?.Invoke(this, _snapshot);
    }

    private void UpdateSnapshot()
    {
        var memory = _memoryScan.MemoryReadObject;
        var snapshot = _snapshot;
        snapshot.ProcessId = memory.ProcessId;
        snapshot.Username = memory.PlayerName;
        snapshot.PlayMods = memory.Mods;
        snapshot.IsReplay = memory.IsReplay;
        snapshot.Score = memory.Score;
        snapshot.Combo = memory.Combo;
        snapshot.Statistics = memory.Statistics;
        snapshot.HitErrors = memory.HitErrors;
        snapshot.Beatmap = memory.BeatmapIdentifier;
        snapshot.BeatmapResourceCatalog = null;
        snapshot.PlayTime = memory.PlayingTime;
        snapshot.Status = memory.OsuStatus;
    }
}