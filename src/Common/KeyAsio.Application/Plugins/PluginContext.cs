using KeyAsio.Plugins.Contracts;
using Microsoft.Extensions.Logging;

namespace KeyAsio.Application.Plugins;

public class PluginContext : IPluginContext
{
    public PluginContext(
        string pluginDirectory,
        ILoggerFactory loggerFactory,
        IAudioEngine audioEngine,
        IPluginSettings settings,
        IGameplaySession gameplay,
        IPluginInteractionService interaction)
    {
        PluginDirectory = pluginDirectory;
        LoggerFactory = loggerFactory;
        AudioEngine = audioEngine;
        Settings = settings;
        Gameplay = gameplay;
        Interaction = interaction;
    }

    public string PluginDirectory { get; }
    public ILoggerFactory LoggerFactory { get; }
    public IAudioEngine AudioEngine { get; }
    public IPluginSettings Settings { get; }
    public IGameplaySession Gameplay { get; }
    public IPluginInteractionService Interaction { get; }

    private readonly Dictionary<SyncOsuStatus, List<IGameStateHandler>> _stateHandlers = new();

    public void RegisterStateHandler(SyncOsuStatus status, IGameStateHandler handler)
    {
        if (!_stateHandlers.TryGetValue(status, out var list))
        {
            list = new List<IGameStateHandler>();
            _stateHandlers[status] = list;
        }

        // Avoid duplicate registration of same instance
        if (!list.Contains(handler))
        {
            list.Add(handler);
        }
    }

    public void UnregisterStateHandler(SyncOsuStatus status)
    {
        _stateHandlers.Remove(status);
    }

    internal IReadOnlyList<IGameStateHandler> GetHandlers(SyncOsuStatus status)
    {
        if (_stateHandlers.TryGetValue(status, out var list))
        {
            return list;
        }

        return Array.Empty<IGameStateHandler>();
    }
}
