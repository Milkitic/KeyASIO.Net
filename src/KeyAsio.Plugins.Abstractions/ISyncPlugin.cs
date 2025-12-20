using System;

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

public enum OsuMemoryStatus
{
    NotRunning = -1,
    Playing = 0,
    SongSelection = 1,
    ResultsScreen = 2,
    MultiplayerRoom = 3,
    EditSongSelection = 4,
    MainView = 5,
    MultiSongSelection = 6
}

public class BeatmapIdentifier
{
    public int SetId { get; set; }
    public int MapId { get; set; }
    public string? Md5 { get; set; }
    public string? Folder { get; set; }
    public string? Filename { get; set; }
}
