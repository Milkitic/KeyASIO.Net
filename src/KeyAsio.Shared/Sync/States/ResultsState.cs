using KeyAsio.Shared.OsuMemory;
using KeyAsio.Shared.Sync.Services;

namespace KeyAsio.Shared.Sync.States;

public class ResultsState : IGameState
{
    private readonly BackgroundMusicManager _backgroundMusicManager;

    public ResultsState(BackgroundMusicManager backgroundMusicManager)
    {
        _backgroundMusicManager = backgroundMusicManager;
    }

    public Task EnterAsync(SyncSessionContext ctx, OsuMemoryStatus from)
    {
        _backgroundMusicManager.SetSingleTrackPlayMods(Mods.None);
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