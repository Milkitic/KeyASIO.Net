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
    /// <returns>True to stop propagation to lower priority handlers and default logic.</returns>
    bool OnEnter(ISyncContext context);

    /// <summary>
    /// Called when the state updates
    /// </summary>
    /// <returns>True to stop propagation to lower priority handlers and default logic.</returns>
    bool OnTick(ISyncContext context);

    /// <summary>
    /// Called when exiting the state
    /// </summary>
    /// <returns>True to stop propagation to lower priority handlers and default logic.</returns>
    bool OnExit(ISyncContext context);
}
