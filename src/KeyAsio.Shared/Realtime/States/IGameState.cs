using KeyAsio.Shared.OsuMemory;

namespace KeyAsio.Shared.Realtime.States;

public interface IGameState
{
    Task EnterAsync(RealtimeSessionContext ctx, OsuMemoryStatus from);
    void Exit(RealtimeSessionContext ctx, OsuMemoryStatus to);
    Task OnPlayTimeChanged(RealtimeSessionContext ctx, int oldMs, int newMs, bool paused);
    void OnComboChanged(RealtimeSessionContext ctx, int oldCombo, int newCombo);
    void OnBeatmapChanged(RealtimeSessionContext ctx, BeatmapIdentifier beatmap);
    void OnModsChanged(RealtimeSessionContext ctx, Mods oldMods, Mods newMods);
}