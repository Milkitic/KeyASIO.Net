namespace KeyAsio.Plugins.Contracts;

/// <summary>
/// Interface for plugins that provide a user interface component to be injected into the main application.
/// </summary>
public interface IUserInterfacePlugin : IPlugin
{
    /// <summary>
    /// Creates the plugin-owned view. The desktop host decides whether the returned
    /// object is a supported presentation element.
    /// </summary>
    object CreateView();
}
