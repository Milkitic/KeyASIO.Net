using KeyAsio.MemoryReading;
using KeyAsio.Shared.Realtime.Services;
using OsuMemoryDataProvider;

namespace KeyAsio.Shared.Realtime.States;

public class ResultsState : IRealtimeState
{
    private readonly MusicTrackService _musicTrackService;

    public ResultsState(MusicTrackService musicTrackService)
    {
        _musicTrackService = musicTrackService;
    }

    public Task EnterAsync(RealtimeModeManager ctx, OsuMemoryStatus from)
    {
        _musicTrackService.SetResultFlag(true);
        _musicTrackService.SetSingleTrackPlayMods(Mods.None);
        return Task.CompletedTask;
    }

    public void Exit(RealtimeModeManager ctx, OsuMemoryStatus to)
    {
    }

    public async Task OnPlayTimeChanged(RealtimeModeManager ctx, int oldMs, int newMs, bool paused)
    {
    }

    public void OnComboChanged(RealtimeModeManager ctx, int oldCombo, int newCombo)
    {
    }

    public void OnBeatmapChanged(RealtimeModeManager ctx, BeatmapIdentifier beatmap)
    {
    }

    public void OnModsChanged(RealtimeModeManager ctx, Mods oldMods, Mods newMods)
    {
    }
}