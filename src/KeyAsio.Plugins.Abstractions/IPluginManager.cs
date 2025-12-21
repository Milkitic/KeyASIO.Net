﻿using System.Collections.Generic;
using System.IO;

namespace KeyAsio.Plugins.Abstractions;

/// <summary>
/// Plugin manager interface
/// </summary>
public interface IPluginManager
{
    /// <summary>
    /// Gets all loaded plugins
    /// </summary>
    IEnumerable<IPlugin> GetAllPlugins();

    /// <summary>
    /// Gets a plugin of the specified type
    /// </summary>
    T? GetPlugin<T>() where T : class, IPlugin;

    /// <summary>
    /// Gets the active handler for the specified state (if any plugin registered an override)
    /// </summary>
    /// <param name="status">Game state</param>
    /// <returns>Handler instance or null</returns>
    IGameStateHandler? GetActiveHandler(SyncOsuStatus status);

    /// <summary>
    /// Loads plugins from the specified directory
    /// </summary>
    /// <param name="pluginDirectory">Plugin directory path</param>
    /// <param name="searchPattern">Search pattern, default is "*.dll"</param>
    /// <param name="searchOption">Search option, default is SearchOption.AllDirectories</param>
    void LoadPlugins(string pluginDirectory, string searchPattern = "*.dll", SearchOption searchOption = SearchOption.AllDirectories);

    /// <summary>
    /// Initializes all plugins
    /// </summary>
    /// <param name="audioEngine">Audio engine</param>
    void InitializePlugins(IAudioEngine audioEngine);

    /// <summary>
    /// Starts all plugins
    /// </summary>
    void StartupPlugins();

    /// <summary>
    /// Unloads all plugins
    /// </summary>
    void UnloadPlugins();
}
