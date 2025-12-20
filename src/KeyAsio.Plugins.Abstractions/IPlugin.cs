namespace KeyAsio.Plugins.Abstractions;

/// <summary>
/// Base plugin interface, all plugins must implement this interface
/// </summary>
public interface IPlugin
{
    /// <summary>
    /// Plugin unique identifier
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Plugin display name
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Plugin version
    /// </summary>
    string Version { get; }

    /// <summary>
    /// Plugin author
    /// </summary>
    string Author { get; }

    /// <summary>
    /// Plugin description
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Initialize the plugin, perform service registration and configuration reading at this stage
    /// </summary>
    /// <param name="context">Plugin context</param>
    void Initialize(IPluginContext context);

    /// <summary>
    /// Start the plugin, perform resource loading and start services at this stage
    /// </summary>
    void Startup();

    /// <summary>
    /// Stop the plugin, stop services at this stage
    /// </summary>
    void Shutdown();

    /// <summary>
    /// Unload the plugin, release all resources at this stage
    /// </summary>
    void Unload();
}
