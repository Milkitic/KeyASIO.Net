namespace KeyAsio.Plugins.Abstractions;

/// <summary>
/// Game state handler interface
/// </summary>
public interface IGameStateHandler
{
    /// <summary>
    /// Called when entering the state
    /// </summary>
    void OnEnter(ISyncContext context);

    /// <summary>
    /// Called when the state updates
    /// </summary>
    void OnTick(ISyncContext context);

    /// <summary>
    /// Called when exiting the state
    /// </summary>
    void OnExit(ISyncContext context);
}

public interface ISyncContext
{
    /// <summary>
    /// Current play time (ms)
    /// </summary>
    int PlayTime { get; }

    /// <summary>
    /// Whether paused
    /// </summary>
    bool IsPaused { get; }

    /// <summary>
    /// Whether started (Gameplay session active)
    /// </summary>
    bool IsStarted { get; }

    /// <summary>
    /// Current game state
    /// </summary>
    OsuMemoryStatus OsuStatus { get; }

    /// <summary>
    /// Timestamp of last update (Ticks)
    /// </summary>
    long LastUpdateTimestamp { get; }

    /// <summary>
    /// Current mods (Bitmask)
    /// </summary>
    int PlayMods { get; }

    /// <summary>
    /// Current beatmap information
    /// </summary>
    BeatmapIdentifier? Beatmap { get; }
}
