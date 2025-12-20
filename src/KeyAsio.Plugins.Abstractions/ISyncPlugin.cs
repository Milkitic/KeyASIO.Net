namespace KeyAsio.Plugins.Abstractions;

/// <summary>
/// Sync plugin interface, used to take over SyncController events and logic
/// </summary>
public interface ISyncPlugin : IPlugin
{
    /// <summary>
    /// Called when the sync loop starts
    /// </summary>
    void OnSyncStart();

    /// <summary>
    /// Called when the sync loop stops
    /// </summary>
    void OnSyncStop();

    /// <summary>
    /// Called every sync frame (high frequency call, pay attention to performance)
    /// </summary>
    /// <param name="context">Sync context</param>
    /// <param name="deltaMs">Time difference from the previous frame (ms)</param>
    void OnTick(ISyncContext context, int deltaMs);

    /// <summary>
    /// Called when the game status changes
    /// </summary>
    void OnStatusChanged(OsuMemoryStatus oldStatus, OsuMemoryStatus newStatus);

    /// <summary>
    /// Called when the beatmap changes
    /// </summary>
    void OnBeatmapChanged(BeatmapIdentifier beatmap);
}