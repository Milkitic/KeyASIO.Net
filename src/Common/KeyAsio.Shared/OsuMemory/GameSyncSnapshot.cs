using KeyAsio.Core.OsuAudio.Hitsounds;
using KeyAsio.Plugins.Abstractions;
using KeyAsio.Plugins.Abstractions.OsuMemory;
using KeyAsio.Shared.Sync;

namespace KeyAsio.Shared.OsuMemory;

/// <summary>
/// Mutable game sync snapshot. A single instance is reused per sync source to
/// avoid per-frame allocations on the high-frequency memory-scan / IPC paths.
/// Consumers must read fields synchronously inside <see cref="IGameSyncSource.SnapshotReceived"/>
/// or <see cref="IGameSyncSource.CurrentSnapshot"/> and must not retain the
/// reference across invocations.
/// </summary>
public sealed class GameSyncSnapshot
{
    public GameClientType ClientType { get; set; } = GameClientType.Stable;
    public int ProcessId { get; set; }
    public string? Username { get; set; }
    public Mods PlayMods { get; set; }
    public bool IsReplay { get; set; }
    public int Score { get; set; }
    public int Combo { get; set; }
    public SyncStatistics Statistics { get; set; } = SyncStatistics.Empty;
    public SyncHitErrors HitErrors { get; set; } = SyncHitErrors.Empty;
    public BeatmapIdentifier Beatmap { get; set; }
    public IBeatmapResourceCatalog? BeatmapResourceCatalog { get; set; }
    public int PlayTime { get; set; }
    public OsuMemoryStatus Status { get; set; } = OsuMemoryStatus.Unknown;

    public static GameSyncSnapshot NotRunning(GameClientType clientType) => new()
    {
        ClientType = clientType,
        Status = OsuMemoryStatus.NotRunning,
        Statistics = SyncStatistics.Empty,
        HitErrors = SyncHitErrors.Empty
    };

    /// <summary>
    /// Resets this instance to a disconnected state in place, avoiding a new
    /// allocation on the low-frequency disconnect path.
    /// </summary>
    public void ResetToNotRunning(GameClientType clientType)
    {
        ClientType = clientType;
        ProcessId = 0;
        Username = null;
        PlayMods = default;
        IsReplay = false;
        Score = 0;
        Combo = 0;
        Statistics = SyncStatistics.Empty;
        HitErrors = SyncHitErrors.Empty;
        Beatmap = default;
        BeatmapResourceCatalog = null;
        PlayTime = 0;
        Status = OsuMemoryStatus.NotRunning;
    }
}