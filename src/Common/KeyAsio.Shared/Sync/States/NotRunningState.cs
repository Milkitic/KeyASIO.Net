using KeyAsio.Plugins.Abstractions.OsuMemory;
using KeyAsio.Shared.OsuMemory;

namespace KeyAsio.Shared.Sync.States;

public class NotRunningState : IGameState
{
    public NotRunningState()
    {
    }

    public Task EnterAsync(SyncSessionContext ctx, OsuMemoryStatus from)
    {
        return Task.CompletedTask;
    }

    public void Exit(SyncSessionContext ctx, OsuMemoryStatus to)
    {
    }

    public void OnTick(SyncSessionContext ctx, int prevMs, int currMs, bool isPaused)
    {
    }

    public void OnComboChanged(SyncSessionContext ctx, int oldCombo, int newCombo)
    {
    }

    public void OnBeatmapChanged(SyncSessionContext ctx, BeatmapIdentifier beatmap)
    {
    }

    public void OnModsChanged(SyncSessionContext ctx, Mods oldMods, Mods newMods)
    {
    }
}