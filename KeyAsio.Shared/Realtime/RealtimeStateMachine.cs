using KeyAsio.Shared.Realtime.States;
using OsuMemoryDataProvider;

namespace KeyAsio.Shared.Realtime;

public class RealtimeStateMachine
{
    private readonly Dictionary<OsuMemoryStatus, IRealtimeState> _states;

    public IRealtimeState? Current { get; private set; }
    public OsuMemoryStatus CurrentStatus { get; private set; }

    public RealtimeStateMachine(Dictionary<OsuMemoryStatus, IRealtimeState> states)
    {
        _states = states;
        CurrentStatus = OsuMemoryStatus.NotRunning;
    }

    public async Task TransitionToAsync(RealtimeProperties ctx, OsuMemoryStatus next)
    {
        var from = CurrentStatus;
        if (!_states.TryGetValue(next, out var target))
        {
            // fallback to Browsing/SongSelect if unknown status
            if (_states.TryGetValue(OsuMemoryStatus.SongSelect, out var songSelect))
            {
                Current?.Exit(ctx, next);
                Current = songSelect;
                CurrentStatus = OsuMemoryStatus.SongSelect;
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