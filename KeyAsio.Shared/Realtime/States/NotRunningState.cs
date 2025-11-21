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

    public Task EnterAsync(RealtimeProperties ctx, OsuMemoryStatus from)
    {
        if (_appSettings.RealtimeOptions.EnableMusicFunctions)
        {
            _musicTrackService.StopCurrentMusic(2000);
        }

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