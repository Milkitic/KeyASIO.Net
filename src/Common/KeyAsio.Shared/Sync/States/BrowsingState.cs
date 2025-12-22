using KeyAsio.Plugins.Abstractions.OsuMemory;
using KeyAsio.Shared.OsuMemory;
using KeyAsio.Shared.Sync.Services;

namespace KeyAsio.Shared.Sync.States;

public class BrowsingState : IGameState
{
    private readonly GameplaySessionManager _gameplaySessionManager;

    public BrowsingState(GameplaySessionManager gameplaySessionManager)
    {
        _gameplaySessionManager = gameplaySessionManager;
    }

    public Task EnterAsync(SyncSessionContext ctx, OsuMemoryStatus from)
    {
        _gameplaySessionManager.Stop();
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