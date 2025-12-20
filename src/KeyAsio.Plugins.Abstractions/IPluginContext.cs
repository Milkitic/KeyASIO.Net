namespace KeyAsio.Plugins.Abstractions;

/// <summary>
/// Plugin context, providing core system access capabilities
/// </summary>
public interface IPluginContext
{
    /// <summary>
    /// Gets the system service provider
    /// </summary>
    IServiceProvider ServiceProvider { get; }

    /// <summary>
    /// Gets the audio engine access interface
    /// </summary>
    IAudioEngine AudioEngine { get; }

    /// <summary>
    /// Registers a state handler, allowing plugins to take over logic for specific states
    /// </summary>
    /// <param name="status">Game state</param>
    /// <param name="handler">Handler</param>
    void RegisterStateHandler(OsuMemoryStatus status, IGameStateHandler handler);

    /// <summary>
    /// Unregisters a state handler
    /// </summary>
    void UnregisterStateHandler(OsuMemoryStatus status);
}
