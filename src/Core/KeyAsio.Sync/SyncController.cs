using System.Diagnostics;
using KeyAsio.Configuration;
using KeyAsio.Core.Audio;
using KeyAsio.Core.Memory.Utils;
using KeyAsio.Plugins.Contracts.Sync;
using KeyAsio.Sync.Abstractions;
using KeyAsio.Sync.AudioProviders;
using KeyAsio.Sync.Services;
using KeyAsio.Sync.Sources;
using KeyAsio.Sync.States;
using Microsoft.Extensions.Logging;

namespace KeyAsio.Sync;

public class SyncController : IDisposable
{
    private readonly SyncSessionContext _syncSessionContext;
    private readonly GameStateMachine _stateMachine;
    private readonly ISyncExtensionHost _extensionHost;

    public SyncController(ILogger<PlayingState> playingStateLogger,
        ILogger<StandardHitsoundSequencer> standardSequencerLogger,
        ILogger<TaikoHitsoundSequencer> taikoSequencerLogger,
        ILogger<ManiaHitsoundSequencer> maniaSequencerLogger,
        ILogger<CatchHitsoundSequencer> catchSequencerLogger,
        AppSettings appSettings,
        IPlaybackEngine playbackEngine,
        IPlaybackRuntimeState runtimeState,
        GameplayAudioService gameplayAudioService,
        BeatmapHitsoundLoader beatmapHitsoundLoader,
        SfxPlaybackService sfxPlaybackService,
        GameplaySessionManager gameplaySessionManager,
        SyncSessionContext syncSessionContext,
        ISyncExtensionHost extensionHost)
    {
        _syncSessionContext = syncSessionContext;
        _extensionHost = extensionHost;

        _syncSessionContext.OnBeatmapChanged = OnBeatmapChanged;
        _syncSessionContext.OnComboChanged = OnComboChanged;
        _syncSessionContext.OnStatusChanged = OnStatusChanged;
        _syncSessionContext.OnPlayModsChanged = OnPlayModsChanged;

        var standardAudioProvider = new StandardHitsoundSequencer(
            standardSequencerLogger,
            appSettings, syncSessionContext, playbackEngine, gameplayAudioService, gameplaySessionManager);
        var taikoAudioProvider = new TaikoHitsoundSequencer(
            taikoSequencerLogger,
            appSettings, syncSessionContext, playbackEngine, gameplayAudioService, gameplaySessionManager);
        var maniaAudioProvider = new ManiaHitsoundSequencer(
            maniaSequencerLogger,
            appSettings, syncSessionContext, playbackEngine, gameplayAudioService, gameplaySessionManager);
        var catchAudioProvider = new CatchHitsoundSequencer(
            catchSequencerLogger,
            appSettings, syncSessionContext, playbackEngine, gameplayAudioService, gameplaySessionManager);
        gameplaySessionManager.InitializeProviders(standardAudioProvider, taikoAudioProvider, catchAudioProvider,
            maniaAudioProvider);

        // Initialize realtime state machine with scene mappings
        _stateMachine = new GameStateMachine(new Dictionary<OsuMemoryStatus, IGameState>
        {
            [OsuMemoryStatus.Playing] = new PlayingState(playingStateLogger, appSettings, playbackEngine,
                beatmapHitsoundLoader, sfxPlaybackService, runtimeState, gameplaySessionManager,
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

        _extensionHost.Start();
        Task.Factory.StartNew(() => RunSyncLoop(token), token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }

    public void Stop()
    {
        _syncLoopCts?.Cancel();
        _syncLoopCts?.Dispose();
        _syncLoopCts = null;

        _extensionHost.Stop();
    }

    private void RunSyncLoop(CancellationToken token)
    {
        using var highPrecisionTimerScope = new HighPrecisionTimerScope();

        const long intervalMs = 2; // 500Hz
        var stopwatch = Stopwatch.StartNew();
        var nextTrigger = stopwatch.ElapsedMilliseconds;
        var oldTime = _syncSessionContext.PlayTime;

        while (!token.IsCancellationRequested)
        {
            var current = stopwatch.ElapsedMilliseconds;
            var wait = nextTrigger - current;

            if (wait > 0)
            {
                Thread.Sleep(Math.Max(0, (int)wait));
            }

            var newTime = _syncSessionContext.PlayTime;
            var blockBase = _extensionHost.HandleTick(newTime - oldTime, _syncSessionContext.OsuStatus);

            if (!blockBase)
            {
                _stateMachine.Current?.OnTick(_syncSessionContext, oldTime, newTime, _syncSessionContext.IsAudioPaused);
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
        if (!_extensionHost.HandleStateExit(oldStatus))
        {
            _stateMachine.ExitCurrent(_syncSessionContext, newStatus);
        }

        if (!_extensionHost.HandleStateEnter(newStatus))
        {
            await _stateMachine.EnterFromAsync(_syncSessionContext, oldStatus, newStatus);
        }

        _extensionHost.NotifyStatusChanged(oldStatus, newStatus);
    }

    private Task OnBeatmapChanged(BeatmapIdentifier oldBeatmap, BeatmapIdentifier newBeatmap)
    {
        if (!_extensionHost.HandleBeatmapChanged(newBeatmap, _syncSessionContext.OsuStatus))
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
