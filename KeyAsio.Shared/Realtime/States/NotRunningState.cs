using KeyAsio.MemoryReading;
using KeyAsio.MemoryReading.OsuMemoryModels;
using KeyAsio.Shared.Realtime.Services;

namespace KeyAsio.Shared.Realtime.States;

public class NotRunningState : IGameState
{
    private readonly AppSettings _appSettings;
    private readonly BackgroundMusicManager _backgroundMusicManager;

    public NotRunningState(AppSettings appSettings, BackgroundMusicManager backgroundMusicManager)
    {
        _appSettings = appSettings;
        _backgroundMusicManager = backgroundMusicManager;
    }

    public Task EnterAsync(RealtimeSessionContext ctx, OsuMemoryStatus from)
    {
        if (_appSettings.Realtime.RealtimeEnableMusic)
        {
            _backgroundMusicManager.StopCurrentMusic(2000);
        }

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