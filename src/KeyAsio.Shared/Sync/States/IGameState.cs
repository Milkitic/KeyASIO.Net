using KeyAsio.Shared.OsuMemory;

namespace KeyAsio.Shared.Sync.States;

public interface IGameState
{
    Task EnterAsync(SyncSessionContext ctx, OsuMemoryStatus from);
    void Exit(SyncSessionContext ctx, OsuMemoryStatus to);

    void OnTick(SyncSessionContext ctx, int prevMs, int currMs, bool isPaused);

    void OnComboChanged(SyncSessionContext ctx, int oldCombo, int newCombo);
    void OnBeatmapChanged(SyncSessionContext ctx, BeatmapIdentifier beatmap);
    void OnModsChanged(SyncSessionContext ctx, Mods oldMods, Mods newMods);
}