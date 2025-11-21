using KeyAsio.MemoryReading;
using KeyAsio.Shared.Realtime.Services;
using OsuMemoryDataProvider;

namespace KeyAsio.Shared.Realtime.States;

public class NotRunningState : IRealtimeState
{
    private readonly AppSettings _appSettings;
    private readonly MusicTrackService _musicTrackService;

    public NotRunningState(AppSettings appSettings, MusicTrackService musicTrackService)
    {
        _appSettings = appSettings;
        _musicTrackService = musicTrackService;
    }

    public Task EnterAsync(IRealtimeContext ctx, OsuMemoryStatus from)
    {
        if (_appSettings.RealtimeOptions.EnableMusicFunctions)
        {
            _musicTrackService.StopCurrentMusic(2000);
        }

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