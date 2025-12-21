using System.Diagnostics;
using KeyAsio.Audio;
using KeyAsio.Audio.Caching;
using KeyAsio.Memory.Utils;
using KeyAsio.Plugins.Abstractions;
using KeyAsio.Shared.Models;
using KeyAsio.Shared.OsuMemory;
using KeyAsio.Shared.Sync.AudioProviders;
using KeyAsio.Shared.Sync.Services;
using KeyAsio.Shared.Sync.States;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OsuMemoryStatus = KeyAsio.Shared.OsuMemory.OsuMemoryStatus;

namespace KeyAsio.Shared.Sync;

public class SyncController : IDisposable
{
    private readonly SyncSessionContext _syncSessionContext;
    private readonly GameStateMachine _stateMachine;
    private readonly IPluginManager _pluginManager;
    private readonly ILogger<SyncController> _logger;

    public SyncController(ILogger<PlayingState> playingStateLogger,
        ILogger<SyncController> logger,
        IServiceProvider serviceProvider,
        AppSettings appSettings,
        AudioEngine audioEngine,
        SharedViewModel sharedViewModel,
        GameplayAudioService gameplayAudioService,
        BeatmapHitsoundLoader beatmapHitsoundLoader,
        BackgroundMusicManager backgroundMusicManager,
        SfxPlaybackService sfxPlaybackService,
        AudioCacheManager audioCacheManager,
        GameplaySessionManager gameplaySessionManager,
        SyncSessionContext syncSessionContext,
        IPluginManager pluginManager)
    {
        _syncSessionContext = syncSessionContext;
        _pluginManager = pluginManager;
        _logger = logger;
        _syncSessionContext.OnBeatmapChanged = OnBeatmapChanged;
        _syncSessionContext.OnComboChanged = OnComboChanged;
        _syncSessionContext.OnStatusChanged = OnStatusChanged;
        _syncSessionContext.OnPlayModsChanged = OnPlayModsChanged;

        var standardAudioProvider = new StandardHitsoundSequencer(
            serviceProvider.GetRequiredService<ILogger<StandardHitsoundSequencer>>(),
            appSettings, syncSessionContext, audioEngine, gameplayAudioService, gameplaySessionManager);
        var maniaAudioProvider = new ManiaHitsoundSequencer(
            serviceProvider.GetRequiredService<ILogger<ManiaHitsoundSequencer>>(),
            appSettings, syncSessionContext, audioEngine, gameplayAudioService, gameplaySessionManager);
        gameplaySessionManager.InitializeProviders(standardAudioProvider, maniaAudioProvider);

        // Initialize realtime state machine with scene mappings
        _stateMachine = new GameStateMachine(new Dictionary<OsuMemoryStatus, IGameState>
        {
            [OsuMemoryStatus.Playing] = new PlayingState(playingStateLogger, appSettings, audioEngine,
                audioCacheManager, backgroundMusicManager, beatmapHitsoundLoader, sfxPlaybackService, sharedViewModel,
                gameplaySessionManager, gameplayAudioService),
            [OsuMemoryStatus.ResultsScreen] = new ResultsState(backgroundMusicManager),
            [OsuMemoryStatus.NotRunning] = new NotRunningState(appSettings, backgroundMusicManager),
            [OsuMemoryStatus.SongSelection] =
                new BrowsingState(appSettings, backgroundMusicManager, gameplaySessionManager),
            [OsuMemoryStatus.EditSongSelection] =
                new BrowsingState(appSettings, backgroundMusicManager, gameplaySessionManager),
            [OsuMemoryStatus.MainView] = new BrowsingState(appSettings, backgroundMusicManager, gameplaySessionManager),
            [OsuMemoryStatus.MultiSongSelection] =
                new BrowsingState(appSettings, backgroundMusicManager, gameplaySessionManager),
        });
    }

    private CancellationTokenSource? _syncLoopCts;

    public void Start()
    {
        if (_syncLoopCts != null) return;
        _syncLoopCts = new CancellationTokenSource();
        var token = _syncLoopCts.Token;

        var syncPlugins = _pluginManager.GetAllPlugins().OfType<ISyncPlugin>().ToList();
        foreach (var plugin in syncPlugins)
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

        Task.Factory.StartNew(() => RunSyncLoop(token, syncPlugins), token, TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
    }

    public void Stop()
    {
        _syncLoopCts?.Cancel();
        _syncLoopCts?.Dispose();
        _syncLoopCts = null;

        var syncPlugins = _pluginManager.GetAllPlugins().OfType<ISyncPlugin>().ToList();
        foreach (var plugin in syncPlugins)
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
        IGameStateHandler? cachedHandler = null;
        var cachedStatus = (KeyAsio.Plugins.Abstractions.SyncOsuStatus)(-2); // Invalid initial status

        while (!token.IsCancellationRequested)
        {
            var current = stopwatch.ElapsedMilliseconds;
            var wait = nextTrigger - current;

            if (wait > 0)
            {
                Thread.Sleep(Math.Max(0, (int)wait));
            }

            var newTime = _syncSessionContext.PlayTime;
            var currentStatus = (KeyAsio.Plugins.Abstractions.SyncOsuStatus)_syncSessionContext.OsuStatus;

            // Update cache if status changed
            if (currentStatus != cachedStatus)
            {
                cachedHandler = _pluginManager.GetActiveHandler(currentStatus);
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
            if (cachedHandler != null)
            {
                cachedHandler.OnTick(contextWrapper);
            }
            else
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
        if (_pluginManager.GetActiveHandler(
                (KeyAsio.Plugins.Abstractions.SyncOsuStatus)_syncSessionContext.OsuStatus) == null)
        {
            _stateMachine.Current?.OnComboChanged(_syncSessionContext, oldCombo, newCombo);
        }

        return Task.CompletedTask;
    }

    private async Task OnStatusChanged(OsuMemoryStatus oldStatus, OsuMemoryStatus newStatus)
    {
        var contextWrapper = new SyncContextWrapper(_syncSessionContext);
        var oldOverride = _pluginManager.GetActiveHandler((KeyAsio.Plugins.Abstractions.SyncOsuStatus)oldStatus);
        var newOverride = _pluginManager.GetActiveHandler((KeyAsio.Plugins.Abstractions.SyncOsuStatus)newStatus);

        // 1. Exit Old State
        if (oldOverride != null)
        {
            try
            {
                oldOverride.OnExit(contextWrapper);
            }
            catch
            {
                // Ignore
            }
        }
        else if (newOverride != null)
        {
            // If going to Override from Default, exit Default
            _stateMachine.ExitCurrent(_syncSessionContext, newStatus);
        }

        // 2. Enter New State
        if (newOverride != null)
        {
            try
            {
                newOverride.OnEnter(contextWrapper);
            }
            catch
            {
                // Ignore
            }
        }
        else
        {
            if (oldOverride != null)
            {
                // Coming from Override -> Default
                await _stateMachine.EnterFromAsync(_syncSessionContext, oldStatus, newStatus);
            }
            else
            {
                // Default -> Default
                await _stateMachine.TransitionToAsync(_syncSessionContext, newStatus);
            }
        }

        var plugins = _pluginManager.GetAllPlugins().OfType<ISyncPlugin>();
        foreach (var plugin in plugins)
        {
            try
            {
                plugin.OnStatusChanged((KeyAsio.Plugins.Abstractions.SyncOsuStatus)oldStatus,
                    (KeyAsio.Plugins.Abstractions.SyncOsuStatus)newStatus);
            }
            catch
            {
                // Ignore
            }
        }
    }

    private Task OnBeatmapChanged(KeyAsio.Shared.OsuMemory.BeatmapIdentifier oldBeatmap,
        KeyAsio.Shared.OsuMemory.BeatmapIdentifier newBeatmap)
    {
        if (_pluginManager.GetActiveHandler(
                (KeyAsio.Plugins.Abstractions.SyncOsuStatus)_syncSessionContext.OsuStatus) == null)
        {
            _stateMachine.Current?.OnBeatmapChanged(_syncSessionContext, newBeatmap);
        }

        var plugins = _pluginManager.GetAllPlugins().OfType<ISyncPlugin>();
        var absBeatmap = new KeyAsio.Plugins.Abstractions.SyncBeatmapInfo
        {
            Folder = newBeatmap.Folder,
            Filename = newBeatmap.Filename
        };

        foreach (var plugin in plugins)
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

        return Task.CompletedTask;
    }

    private Task OnPlayModsChanged(Mods oldMods, Mods newMods)
    {
        if (_pluginManager.GetActiveHandler(
                (KeyAsio.Plugins.Abstractions.SyncOsuStatus)_syncSessionContext.OsuStatus) == null)
        {
            _stateMachine.Current?.OnModsChanged(_syncSessionContext, oldMods, newMods);
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        Stop();
    }
}