using KeyAsio.Shared.OsuMemory;
using KeyAsio.Shared.Realtime.States;

namespace KeyAsio.Shared.Realtime;

public class GameStateMachine
{
    private readonly Dictionary<OsuMemoryStatus, IGameState> _states;

    public IGameState? Current { get; private set; }
    public OsuMemoryStatus CurrentStatus { get; private set; }

    public GameStateMachine(Dictionary<OsuMemoryStatus, IGameState> states)
    {
        _states = states;
        CurrentStatus = OsuMemoryStatus.NotRunning;
    }

    public async Task TransitionToAsync(RealtimeSessionContext ctx, OsuMemoryStatus next)
    {
        var from = CurrentStatus;
        if (!_states.TryGetValue(next, out var target))
        {
            // fallback to Browsing/SongSelect if unknown status
            if (_states.TryGetValue(OsuMemoryStatus.SongSelection, out var songSelect))
            {
                Current?.Exit(ctx, next);
                Current = songSelect;
                CurrentStatus = OsuMemoryStatus.SongSelection;
                await songSelect.EnterAsync(ctx, from);
            }

            return;
        }

        Current?.Exit(ctx, next);
        Current = target;
        CurrentStatus = next;
        await target.EnterAsync(ctx, from);
    }
}