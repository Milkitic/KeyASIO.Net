using System.Diagnostics;
using KeyAsio.Core.Audio;
using KeyAsio.Core.Memory.Utils;
using KeyAsio.Plugins.Abstractions;
using KeyAsio.Plugins.Abstractions.OsuMemory;
using KeyAsio.Shared.Models;
using KeyAsio.Shared.OsuMemory;
using KeyAsio.Shared.Sync.AudioProviders;
using KeyAsio.Shared.Sync.Services;
using KeyAsio.Shared.Sync.States;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KeyAsio.Shared.Sync;

public class SyncController : IDisposable
{
    private readonly SyncSessionContext _syncSessionContext;
    private readonly GameStateMachine _stateMachine;
    private readonly IPluginManager _pluginManager;
    private readonly ILogger<SyncController> _logger;
    private readonly List<ISyncPlugin> _activeSyncPlugins;

    public SyncController(ILogger<PlayingState> playingStateLogger,
        ILogger<SyncController> logger,
        IServiceProvider serviceProvider,
        AppSettings appSettings,
        IPlaybackEngine playbackEngine,
        SharedViewModel sharedViewModel,
        GameplayAudioService gameplayAudioService,
        BeatmapHitsoundLoader beatmapHitsoundLoader,
        SfxPlaybackService sfxPlaybackService,
        GameplaySessionManager gameplaySessionManager,
        SyncSessionContext syncSessionContext,
        IPluginManager pluginManager)
    {
        _syncSessionContext = syncSessionContext;
        _pluginManager = pluginManager;
        _logger = logger;

        _activeSyncPlugins = _pluginManager.GetAllPlugins().OfType<ISyncPlugin>().ToList();

        _syncSessionContext.OnBeatmapChanged = OnBeatmapChanged;
        _syncSessionContext.OnComboChanged = OnComboChanged;
        _syncSessionContext.OnStatusChanged = OnStatusChanged;
        _syncSessionContext.OnPlayModsChanged = OnPlayModsChanged;

        var standardAudioProvider = new StandardHitsoundSequencer(
            serviceProvider.GetRequiredService<ILogger<StandardHitsoundSequencer>>(),
            appSettings, syncSessionContext, playbackEngine, gameplayAudioService, gameplaySessionManager);
        var taikoAudioProvider = new TaikoHitsoundSequencer(
            serviceProvider.GetRequiredService<ILogger<TaikoHitsoundSequencer>>(),
            appSettings, syncSessionContext, playbackEngine, gameplayAudioService, gameplaySessionManager);
        var maniaAudioProvider = new ManiaHitsoundSequencer(
            serviceProvider.GetRequiredService<ILogger<ManiaHitsoundSequencer>>(),
            appSettings, syncSessionContext, playbackEngine, gameplayAudioService, gameplaySessionManager);
        var catchAudioProvider = new CatchHitsoundSequencer(
            serviceProvider.GetRequiredService<ILogger<CatchHitsoundSequencer>>(),
            appSettings, syncSessionContext, playbackEngine, gameplayAudioService, gameplaySessionManager);
        gameplaySessionManager.InitializeProviders(standardAudioProvider, taikoAudioProvider, catchAudioProvider, maniaAudioProvider);

        // Initialize realtime state machine with scene mappings
        _stateMachine = new GameStateMachine(new Dictionary<OsuMemoryStatus, IGameState>
        {
            [OsuMemoryStatus.Playing] = new PlayingState(playingStateLogger, appSettings, playbackEngine,
                beatmapHitsoundLoader, sfxPlaybackService, sharedViewModel, gameplaySessionManager,
                gameplayAudioService),
            [OsuMemoryStatus.ResultsScreen] = new ResultsState(),
            [OsuMemoryStatus.NotRunning] = new NotRunningState(),
            [OsuMemoryStatus.SongSelection] = new BrowsingState(gameplaySessionManager),
            [OsuMemoryStatus.EditSongSelection] = new BrowsingState(gameplaySessionManager),
            [OsuMemoryStatus.MainView] = new BrowsingState(gameplaySessionManager),
            [OsuMemoryStatus.MultiSongSelection] = new BrowsingState(gameplaySessionManager),
        });
    }

    private CancellationTokenSource? _syncLoopCts;

    public void Start()
    {
        if (_syncLoopCts != null) return;
        _syncLoopCts = new CancellationTokenSource();
        var token = _syncLoopCts.Token;

        foreach (var plugin in _activeSyncPlugins)
        {
            try
            {
                plugin.OnSyncStart();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting sync plugin {PluginName} ({PluginId})", plugin.Name, plugin.Id);
            }
        }

        Task.Factory.StartNew(() => RunSyncLoop(token, _activeSyncPlugins), TaskCreationOptions.LongRunning);
    }

    public void Stop()
    {
        _syncLoopCts?.Cancel();
        _syncLoopCts?.Dispose();
        _syncLoopCts = null;

        foreach (var plugin in _activeSyncPlugins)
        {
            try
            {
                plugin.OnSyncStop();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping sync plugin {PluginName} ({PluginId})", plugin.Name, plugin.Id);
            }
        }
    }

    private void RunSyncLoop(CancellationToken token, List<ISyncPlugin> plugins)
    {
        using var highPrecisionTimerScope = new HighPrecisionTimerScope();
        var contextWrapper = new SyncContextWrapper(_syncSessionContext);

        const long intervalMs = 2; // 500Hz
        var stopwatch = Stopwatch.StartNew();
        var nextTrigger = stopwatch.ElapsedMilliseconds;
        var oldTime = _syncSessionContext.PlayTime;

        // Cache variables
        List<IGameStateHandler> cachedHandlers = new();
        var cachedStatus = SyncOsuStatus.Unknown;

        while (!token.IsCancellationRequested)
        {
            var current = stopwatch.ElapsedMilliseconds;
            var wait = nextTrigger - current;

            if (wait > 0)
            {
                Thread.Sleep(Math.Max(0, (int)wait));
            }

            var newTime = _syncSessionContext.PlayTime;
            var currentStatus = (SyncOsuStatus)_syncSessionContext.OsuStatus;

            // Update cache if status changed
            if (currentStatus != cachedStatus)
            {
                cachedHandlers = _pluginManager.GetActiveHandlers(currentStatus).ToList();
                cachedStatus = currentStatus;
            }

            // Invoke plugins
            foreach (var plugin in plugins)
            {
                try
                {
                    plugin.OnTick(contextWrapper, newTime - oldTime);
                }
                catch
                {
                    // Suppress plugin errors to keep loop running
                }
            }

            // Check if any plugin overrides the current state
            bool blockBase = false;
            foreach (var handler in cachedHandlers)
            {
                var result = handler.HandleTick(contextWrapper);
                if ((result & HandleResult.BlockBaseLogic) != 0)
                {
                    blockBase = true;
                }

                if ((result & HandleResult.BlockLowerPriority) != 0)
                {
                    break;
                }
            }

            if (!blockBase)
            {
                _stateMachine.Current?.OnTick(_syncSessionContext, oldTime, newTime, oldTime == newTime);
            }

            oldTime = newTime;

            nextTrigger += intervalMs;

            if (stopwatch.ElapsedMilliseconds > nextTrigger + 50)
            {
                nextTrigger = stopwatch.ElapsedMilliseconds;
            }
        }
    }

    private Task OnComboChanged(int oldCombo, int newCombo)
    {
        _stateMachine.Current?.OnComboChanged(_syncSessionContext, oldCombo, newCombo);
        return Task.CompletedTask;
    }

    private async Task OnStatusChanged(OsuMemoryStatus oldStatus, OsuMemoryStatus newStatus)
    {
        var contextWrapper = new SyncContextWrapper(_syncSessionContext);
        var oldHandlers = _pluginManager.GetActiveHandlers((SyncOsuStatus)oldStatus);
        var newHandlers = _pluginManager.GetActiveHandlers((SyncOsuStatus)newStatus);

        // 1. Exit Old State
        bool blockBaseExit = false;
        foreach (var handler in oldHandlers)
        {
            try
            {
                var result = handler.HandleExit(contextWrapper);
                if ((result & HandleResult.BlockBaseLogic) != 0)
                {
                    blockBaseExit = true;
                }

                if ((result & HandleResult.BlockLowerPriority) != 0)
                {
                    break;
                }
            }
            catch
            {
                // Ignore
            }
        }

        if (!blockBaseExit)
        {
            _stateMachine.ExitCurrent(_syncSessionContext, newStatus);
        }

        // 2. Enter New State
        bool blockBaseEnter = false;
        foreach (var handler in newHandlers)
        {
            try
            {
                var result = handler.HandleEnter(contextWrapper);
                if ((result & HandleResult.BlockBaseLogic) != 0)
                {
                    blockBaseEnter = true;
                }

                if ((result & HandleResult.BlockLowerPriority) != 0)
                {
                    break;
                }
            }
            catch
            {
                // Ignore
            }
        }

        if (!blockBaseEnter)
        {
            await _stateMachine.EnterFromAsync(_syncSessionContext, oldStatus, newStatus);
        }

        foreach (var plugin in _activeSyncPlugins)
        {
            try
            {
                plugin.OnStatusChanged((SyncOsuStatus)oldStatus, (SyncOsuStatus)newStatus);
            }
            catch
            {
                // Ignore
            }
        }
    }

    private Task OnBeatmapChanged(BeatmapIdentifier oldBeatmap, BeatmapIdentifier newBeatmap)
    {
        var absBeatmap = new SyncBeatmapInfo
        {
            Folder = newBeatmap.Folder,
            Filename = newBeatmap.Filename
        };

        // Notify plugins (Legacy behavior, always notified)
        foreach (var plugin in _activeSyncPlugins)
        {
            try
            {
                plugin.OnBeatmapChanged(absBeatmap);
            }
            catch
            {
                // Ignore
            }
        }

        // Notify active handlers
        var handlers = _pluginManager.GetActiveHandlers((SyncOsuStatus)_syncSessionContext.OsuStatus);
        bool blockBase = false;
        foreach (var handler in handlers)
        {
            try
            {
                var result = handler.HandleBeatmapChange(absBeatmap);

                if ((result & HandleResult.BlockBaseLogic) != 0)
                {
                    blockBase = true;
                }

                if ((result & HandleResult.BlockLowerPriority) != 0)
                {
                    break;
                }
            }
            catch
            {
                // Ignore
            }
        }

        if (!blockBase)
        {
            _stateMachine.Current?.OnBeatmapChanged(_syncSessionContext, newBeatmap);
        }

        return Task.CompletedTask;
    }

    private Task OnPlayModsChanged(Mods oldMods, Mods newMods)
    {
        _stateMachine.Current?.OnModsChanged(_syncSessionContext, oldMods, newMods);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        Stop();
    }
}