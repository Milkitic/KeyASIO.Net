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

    public Task EnterAsync(RealtimeProperties ctx, OsuMemoryStatus from)
    {
        _musicTrackService.SetResultFlag(true);
        _musicTrackService.SetSingleTrackPlayMods(Mods.None);
        return Task.CompletedTask;
    }

    public void Exit(RealtimeProperties ctx, OsuMemoryStatus to)
    {
    }

    public async Task OnPlayTimeChanged(RealtimeProperties ctx, int oldMs, int newMs, bool paused)
    {
    }

    public void OnComboChanged(RealtimeProperties ctx, int oldCombo, int newCombo)
    {
    }

    public void OnBeatmapChanged(RealtimeProperties ctx, BeatmapIdentifier beatmap)
    {
    }

    public void OnModsChanged(RealtimeProperties ctx, Mods oldMods, Mods newMods)
    {
    }
}