using Microsoft.Extensions.Logging;

namespace KeyAsio.Plugins.Contracts;

/// <summary>
/// Plugin context, providing core system access capabilities
/// </summary>
public interface IPluginContext
{
    string PluginDirectory { get; }

    ILoggerFactory LoggerFactory { get; }

    /// <summary>
    /// Gets the audio engine access interface
    /// </summary>
    IAudioEngine AudioEngine { get; }

    IPluginSettings Settings { get; }

    IGameplaySession Gameplay { get; }

    IPluginInteractionService Interaction { get; }

    /// <summary>
    /// Registers a state handler, allowing plugins to take over logic for specific states
    /// </summary>
    /// <param name="status">Game state</param>
    /// <param name="handler">Handler</param>
    void RegisterStateHandler(SyncOsuStatus status, IGameStateHandler handler);

    /// <summary>
    /// Unregisters a state handler
    /// </summary>
    void UnregisterStateHandler(SyncOsuStatus status);
}
