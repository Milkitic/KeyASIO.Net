namespace KeyAsio.Plugins.Abstractions;

/// <summary>
/// Game state handler interface
/// </summary>
public interface IGameStateHandler
{
    /// <summary>
    /// Called when entering the state
    /// </summary>
    void OnEnter(ISyncContext context);

    /// <summary>
    /// Called when the state updates
    /// </summary>
    void OnTick(ISyncContext context);

    /// <summary>
    /// Called when exiting the state
    /// </summary>
    void OnExit(ISyncContext context);
}