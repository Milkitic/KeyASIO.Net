using KeyAsio.Shared.OsuMemory;
using KeyAsio.Shared.Sync.States;

namespace KeyAsio.Shared.Sync;

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

    public async Task TransitionToAsync(SyncSessionContext ctx, OsuMemoryStatus next)
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

    public void ExitCurrent(SyncSessionContext ctx, OsuMemoryStatus next)
    {
        Current?.Exit(ctx, next);
        Current = null;
        CurrentStatus = next;
    }

    public async Task EnterFromAsync(SyncSessionContext ctx, OsuMemoryStatus from, OsuMemoryStatus next)
    {
        if (_states.TryGetValue(next, out var target))
        {
            Current = target;
            CurrentStatus = next;
            await target.EnterAsync(ctx, from);
        }
        else
        {
            // Fallback logic similar to TransitionToAsync if needed, or just ignore
            if (_states.TryGetValue(OsuMemoryStatus.SongSelection, out var songSelect))
            {
                Current = songSelect;
                CurrentStatus = OsuMemoryStatus.SongSelection;
                await songSelect.EnterAsync(ctx, from);
            }
        }
    }
}