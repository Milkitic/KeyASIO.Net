using KeyAsio.MemoryReading;
using OsuMemoryDataProvider;

namespace KeyAsio.Shared.Realtime.States;

public class NotRunningState : IRealtimeState
{
    public Task EnterAsync(RealtimeModeManager ctx, OsuMemoryStatus from)
    {
        if (ctx.AppSettings.RealtimeOptions.EnableMusicFunctions)
        {
            ctx.StopCurrentMusic(2000);
        }

        return Task.CompletedTask;
    }

    public void Exit(RealtimeModeManager ctx, OsuMemoryStatus to)
    {
    }

    public void OnPlayTimeChanged(RealtimeModeManager ctx, int oldMs, int newMs, bool paused)
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