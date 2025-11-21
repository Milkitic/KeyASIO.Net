using KeyAsio.MemoryReading;
using OsuMemoryDataProvider;

namespace KeyAsio.Shared.Realtime.States;

public interface IRealtimeState
{
    Task EnterAsync(IRealtimeContext ctx, OsuMemoryStatus from);
    void Exit(IRealtimeContext ctx, OsuMemoryStatus to);
    Task OnPlayTimeChanged(IRealtimeContext ctx, int oldMs, int newMs, bool paused);
    void OnComboChanged(IRealtimeContext ctx, int oldCombo, int newCombo);
    void OnBeatmapChanged(IRealtimeContext ctx, BeatmapIdentifier beatmap);
    void OnModsChanged(IRealtimeContext ctx, Mods oldMods, Mods newMods);
}