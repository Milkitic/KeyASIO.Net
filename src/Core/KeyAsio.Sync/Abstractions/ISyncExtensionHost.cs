using KeyAsio.Plugins.Contracts.Sync;
using KeyAsio.Sync.Sources;

namespace KeyAsio.Sync.Abstractions;

/// <summary>
/// Hosts optional extensions around the synchronization state machine.
/// Implementations belong to the application layer; the sync domain only owns this port.
/// </summary>
public interface ISyncExtensionHost
{
    void Start();
    void Stop();

    bool HandleTick(int deltaMs, OsuMemoryStatus status);
    bool HandleStateExit(OsuMemoryStatus status);
    bool HandleStateEnter(OsuMemoryStatus status);
    bool HandleBeatmapChanged(BeatmapIdentifier beatmap, OsuMemoryStatus status);

    void NotifyStatusChanged(OsuMemoryStatus oldStatus, OsuMemoryStatus newStatus);
}
