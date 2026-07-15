using KeyAsio.Plugins.Contracts;
using KeyAsio.Plugins.Contracts.Sync;
using KeyAsio.Sync;
using KeyAsio.Sync.Abstractions;
using KeyAsio.Sync.Sources;
using Microsoft.Extensions.Logging;

namespace KeyAsio.Application.Plugins;

/// <summary>
/// Adapts synchronization-domain events to the public plugin contract.
/// </summary>
public sealed class SyncPluginCoordinator : ISyncExtensionHost
{
    private readonly IPluginManager _pluginManager;
    private readonly ILogger<SyncPluginCoordinator> _logger;
    private readonly PluginSyncContextAdapter _context;

    private IReadOnlyList<ISyncPlugin> _plugins = Array.Empty<ISyncPlugin>();
    private IReadOnlyList<IGameStateHandler> _tickHandlers = Array.Empty<IGameStateHandler>();
    private SyncOsuStatus? _tickHandlerStatus;

    public SyncPluginCoordinator(
        IPluginManager pluginManager,
        ILogger<SyncPluginCoordinator> logger,
        SyncSessionContext context)
    {
        _pluginManager = pluginManager;
        _logger = logger;
        _context = new PluginSyncContextAdapter(context);
    }

    public void Start()
    {
        _plugins = _pluginManager.GetAllPlugins().OfType<ISyncPlugin>().ToArray();
        InvokePlugins(static plugin => plugin.OnSyncStart(), "start");
    }

    public void Stop()
    {
        InvokePlugins(static plugin => plugin.OnSyncStop(), "stop");
        _plugins = Array.Empty<ISyncPlugin>();
        _tickHandlers = Array.Empty<IGameStateHandler>();
        _tickHandlerStatus = null;
    }

    public bool HandleTick(int deltaMs, OsuMemoryStatus status)
    {
        InvokePlugins(plugin => plugin.OnTick(_context, deltaMs), "tick");

        var pluginStatus = (SyncOsuStatus)status;
        if (pluginStatus != _tickHandlerStatus)
        {
            _tickHandlers = GetHandlers(pluginStatus);
            _tickHandlerStatus = pluginStatus;
        }

        return DispatchHandlers(_tickHandlers, handler => handler.HandleTick(_context), "tick");
    }

    public bool HandleStateExit(OsuMemoryStatus status) =>
        DispatchHandlers(GetHandlers((SyncOsuStatus)status), handler => handler.HandleExit(_context), "exit");

    public bool HandleStateEnter(OsuMemoryStatus status) =>
        DispatchHandlers(GetHandlers((SyncOsuStatus)status), handler => handler.HandleEnter(_context), "enter");

    public bool HandleBeatmapChanged(BeatmapIdentifier beatmap, OsuMemoryStatus status)
    {
        var pluginBeatmap = PluginSyncContextAdapter.ToPluginBeatmap(beatmap);
        InvokePlugins(plugin => plugin.OnBeatmapChanged(pluginBeatmap), "beatmap change");
        return DispatchHandlers(
            GetHandlers((SyncOsuStatus)status),
            handler => handler.HandleBeatmapChange(pluginBeatmap),
            "beatmap change");
    }

    public void NotifyStatusChanged(OsuMemoryStatus oldStatus, OsuMemoryStatus newStatus) =>
        InvokePlugins(
            plugin => plugin.OnStatusChanged((SyncOsuStatus)oldStatus, (SyncOsuStatus)newStatus),
            "status change");

    private IReadOnlyList<IGameStateHandler> GetHandlers(SyncOsuStatus status)
    {
        var handlers = _pluginManager.GetActiveHandlers(status);
        return handlers as IReadOnlyList<IGameStateHandler> ?? handlers.ToArray();
    }

    private bool DispatchHandlers(
        IEnumerable<IGameStateHandler> handlers,
        Func<IGameStateHandler, HandleResult> invoke,
        string operation)
    {
        var blockBaseLogic = false;
        foreach (var handler in handlers)
        {
            try
            {
                var result = invoke(handler);
                blockBaseLogic |= (result & HandleResult.BlockBaseLogic) != 0;
                if ((result & HandleResult.BlockLowerPriority) != 0)
                {
                    break;
                }
            }
            catch (Exception exception)
            {
                _logger.LogError(exception,
                    "Sync handler {HandlerType} failed during {Operation}",
                    handler.GetType().FullName,
                    operation);
            }
        }

        return blockBaseLogic;
    }

    private void InvokePlugins(Action<ISyncPlugin> invoke, string operation)
    {
        foreach (var plugin in _plugins)
        {
            try
            {
                invoke(plugin);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception,
                    "Sync plugin {PluginName} ({PluginId}) failed during {Operation}",
                    plugin.Name,
                    plugin.Id,
                    operation);
            }
        }
    }
}
