using KeyAsio.MemoryReading;
using OsuMemoryDataProvider;

namespace KeyAsio.Shared.Realtime.States;

public interface IRealtimeState
{
    Task EnterAsync(RealtimeModeManager ctx, OsuMemoryStatus from);
    void Exit(RealtimeModeManager ctx, OsuMemoryStatus to);
    void OnPlayTimeChanged(RealtimeModeManager ctx, int oldMs, int newMs, bool paused);
    void OnComboChanged(RealtimeModeManager ctx, int oldCombo, int newCombo);
    void OnBeatmapChanged(RealtimeModeManager ctx, BeatmapIdentifier beatmap);
    void OnModsChanged(RealtimeModeManager ctx, Mods oldMods, Mods newMods);
}