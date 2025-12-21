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

    private readonly Dictionary<SyncOsuStatus, IGameStateHandler> _stateHandlers = new();

    public void RegisterStateHandler(SyncOsuStatus status, IGameStateHandler handler)
    {
        _stateHandlers[status] = handler;
        // Logic to hook this into the main game state machine will be handled by the controller
    }

    public void UnregisterStateHandler(SyncOsuStatus status)
    {
        _stateHandlers.Remove(status);
    }

    internal IGameStateHandler? GetHandler(SyncOsuStatus status)
    {
        return _stateHandlers.GetValueOrDefault(status);
    }
}