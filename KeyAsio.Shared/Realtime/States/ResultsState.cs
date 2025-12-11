using KeyAsio.MemoryReading;
using KeyAsio.MemoryReading.OsuMemoryModels;
using KeyAsio.Shared.Realtime.Services;

namespace KeyAsio.Shared.Realtime.States;

public class ResultsState : IGameState
{
    private readonly BackgroundMusicManager _backgroundMusicManager;

    public ResultsState(BackgroundMusicManager backgroundMusicManager)
    {
        _backgroundMusicManager = backgroundMusicManager;
    }

    public Task EnterAsync(RealtimeSessionContext ctx, OsuMemoryStatus from)
    {
        _backgroundMusicManager.SetResultFlag(true);
        _backgroundMusicManager.SetSingleTrackPlayMods(Mods.None);
        return Task.CompletedTask;
    }

    public void Exit(RealtimeSessionContext ctx, OsuMemoryStatus to)
    {
    }

    public async Task OnPlayTimeChanged(RealtimeSessionContext ctx, int oldMs, int newMs, bool paused)
    {
    }

    public void OnComboChanged(RealtimeSessionContext ctx, int oldCombo, int newCombo)
    {
    }

    public void OnBeatmapChanged(RealtimeSessionContext ctx, BeatmapIdentifier beatmap)
    {
    }

    public void OnModsChanged(RealtimeSessionContext ctx, Mods oldMods, Mods newMods)
    {
    }
}