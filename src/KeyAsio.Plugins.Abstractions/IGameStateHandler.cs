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
    HandleResult HandleEnter(ISyncContext context);

    /// <summary>
    /// Called when the state updates
    /// </summary>
    /// <returns>Result controlling the propagation.</returns>
    HandleResult HandleTick(ISyncContext context);

    /// <summary>
    /// Called when exiting the state
    /// </summary>
    /// <returns>Result controlling the propagation.</returns>
    HandleResult HandleExit(ISyncContext context);

    /// <summary>
    /// Called when the beatmap changes
    /// </summary>
    /// <param name="beatmap">New beatmap information</param>
    /// <returns>Result controlling the propagation.</returns>
    HandleResult HandleBeatmapChange(SyncBeatmapInfo beatmap) => HandleResult.Continue;
}