namespace KeyAsio.Plugins.Abstractions;

public interface ISyncContext
{
    /// <summary>
    /// Current play time (ms)
    /// </summary>
    int PlayTime { get; }

    /// <summary>
    /// Whether started (Gameplay session active)
    /// </summary>
    bool IsStarted { get; }

    /// <summary>
    /// Current game state
    /// </summary>
    SyncOsuStatus OsuStatus { get; }

    /// <summary>
    /// Timestamp of last update (Ticks)
    /// </summary>
    long LastUpdateTimestamp { get; }

    /// <summary>
    /// Current mods (Bitmask)
    /// </summary>
    int PlayMods { get; }

    /// <summary>
    /// Gameplay judgement statistics.
    /// </summary>
    SyncStatistics Statistics { get; }

    /// <summary>
    /// Latest hit error stream update.
    /// </summary>
    SyncHitErrors HitErrors { get; }

    /// <summary>
    /// Current beatmap information
    /// </summary>
    SyncBeatmapInfo? Beatmap { get; }
}
