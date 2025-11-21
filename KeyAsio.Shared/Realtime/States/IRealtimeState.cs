using KeyAsio.MemoryReading;
using OsuMemoryDataProvider;

namespace KeyAsio.Shared.Realtime.States;

public interface IRealtimeState
{
    Task EnterAsync(RealtimeProperties ctx, OsuMemoryStatus from);
    void Exit(RealtimeProperties ctx, OsuMemoryStatus to);
    Task OnPlayTimeChanged(RealtimeProperties ctx, int oldMs, int newMs, bool paused);
    void OnComboChanged(RealtimeProperties ctx, int oldCombo, int newCombo);
    void OnBeatmapChanged(RealtimeProperties ctx, BeatmapIdentifier beatmap);
    void OnModsChanged(RealtimeProperties ctx, Mods oldMods, Mods newMods);
}