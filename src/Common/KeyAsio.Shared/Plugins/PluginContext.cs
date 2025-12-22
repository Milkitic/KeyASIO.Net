using KeyAsio.Plugins.Abstractions;

namespace KeyAsio.Shared.Plugins;

public class PluginContext : IPluginContext
{
    public PluginContext(IServiceProvider serviceProvider, IAudioEngine audioEngine, string pluginDirectory)
    {
        ServiceProvider = serviceProvider;
        AudioEngine = audioEngine;
        PluginDirectory = pluginDirectory;
    }

    public IServiceProvider ServiceProvider { get; }
    public IAudioEngine AudioEngine { get; }
    public string PluginDirectory { get; }

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