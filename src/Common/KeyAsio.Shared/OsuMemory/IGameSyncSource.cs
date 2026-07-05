using KeyAsio.Shared.Sync;

namespace KeyAsio.Shared.OsuMemory;

public interface IGameSyncSource
{
    string Name { get; }
    GameClientType ClientType { get; }
    int Priority { get; }
    bool IsAvailable { get; }
    GameSyncSnapshot CurrentSnapshot { get; }

    event Action<IGameSyncSource, bool>? AvailabilityChanged;
    event Action<IGameSyncSource, GameSyncSnapshot>? SnapshotReceived;

    void Start();
    Task StopAsync();
}
