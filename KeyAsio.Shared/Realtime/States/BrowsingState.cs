using KeyAsio.MemoryReading;
using OsuMemoryDataProvider;

namespace KeyAsio.Shared.Realtime.States;

public class BrowsingState : IRealtimeState
{
    public Task EnterAsync(RealtimeModeManager ctx, OsuMemoryStatus from)
    {
        ctx.StartLowPass(200, 16000);
        ctx.SetResultFlag(false);
        ctx.Stop();
        return Task.CompletedTask;
    }

    public void Exit(RealtimeModeManager ctx, OsuMemoryStatus to)
    {
    }

    public void OnPlayTimeChanged(RealtimeModeManager ctx, int oldMs, int newMs, bool paused)
    {
    }

    public void OnBeatmapChanged(RealtimeModeManager ctx, BeatmapIdentifier beatmap)
    {
    }

    public void OnModsChanged(RealtimeModeManager ctx, Mods oldMods, Mods newMods)
    {
    }
}