using Avalonia.Controls;
using KeyAsio.Plugins.Abstractions;

namespace KeyAsio.Shared.Plugins;

/// <summary>
/// Interface for plugins that provide a user interface component to be injected into the main application.
/// </summary>
public interface IUserInterfacePlugin : IPlugin
{
    /// <summary>
    /// Gets the UI control provided by the plugin.
    /// </summary>
    /// <returns>The Avalonia control to be displayed.</returns>
    Control GetPluginControl();
}