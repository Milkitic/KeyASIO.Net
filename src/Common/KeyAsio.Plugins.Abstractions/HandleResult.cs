namespace KeyAsio.Plugins.Abstractions;

/// <summary>
/// Result of a game state handler operation, controlling the propagation of the event.
/// </summary>
[Flags]
public enum HandleResult
{
    /// <summary>
    /// Continue to execute lower priority handlers and the base logic.
    /// </summary>
    Continue = 0,

    /// <summary>
    /// Stop executing lower priority handlers.
    /// </summary>
    BlockLowerPriority = 1 << 0,

    /// <summary>
    /// Block the base logic (default internal logic) from executing.
    /// </summary>
    BlockBaseLogic = 1 << 1,

    /// <summary>
    /// Stop executing both lower priority handlers and the base logic.
    /// </summary>
    BlockAll = BlockLowerPriority | BlockBaseLogic
}