namespace KeyAsio.Plugins.Abstractions;

/// <summary>
/// Game state handler interface
/// </summary>
public interface IGameStateHandler
{
    /// <summary>
    /// Handler priority. Higher values are executed first.
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// Called when entering the state
    /// </summary>
    /// <returns>Result controlling the propagation.</returns>
    HandleResult OnEnter(ISyncContext context);

    /// <summary>
    /// Called when the state updates
    /// </summary>
    /// <returns>Result controlling the propagation.</returns>
    HandleResult OnTick(ISyncContext context);

    /// <summary>
    /// Called when exiting the state
    /// </summary>
    /// <returns>Result controlling the propagation.</returns>
    HandleResult OnExit(ISyncContext context);
}
