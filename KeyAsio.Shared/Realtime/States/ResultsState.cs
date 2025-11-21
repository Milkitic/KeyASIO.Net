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

    public Task EnterAsync(IRealtimeContext ctx, OsuMemoryStatus from)
    {
        _musicTrackService.SetResultFlag(true);
        _musicTrackService.SetSingleTrackPlayMods(Mods.None);
        return Task.CompletedTask;
    }

    public void Exit(IRealtimeContext ctx, OsuMemoryStatus to)
    {
    }

    public async Task OnPlayTimeChanged(IRealtimeContext ctx, int oldMs, int newMs, bool paused)
    {
    }

    public void OnComboChanged(IRealtimeContext ctx, int oldCombo, int newCombo)
    {
    }

    public void OnBeatmapChanged(IRealtimeContext ctx, BeatmapIdentifier beatmap)
    {
    }

    public void OnModsChanged(IRealtimeContext ctx, Mods oldMods, Mods newMods)
    {
    }
}