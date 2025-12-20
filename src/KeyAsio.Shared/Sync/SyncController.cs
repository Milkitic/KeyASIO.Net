using System.Diagnostics;
using KeyAsio.Audio;
using KeyAsio.Audio.Caching;
using KeyAsio.Memory.Utils;
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

    public SyncController(ILogger<PlayingState> playingStateLogger,
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
        SyncSessionContext syncSessionContext)
    {
        _syncSessionContext = syncSessionContext;
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

        Task.Factory.StartNew(() => RunSyncLoop(token), token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }

    public void Stop()
    {
        _syncLoopCts?.Cancel();
        _syncLoopCts?.Dispose();
        _syncLoopCts = null;
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
            _stateMachine.Current?.OnTick(_syncSessionContext, oldTime, newTime, oldTime == newTime);
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
        await _stateMachine.TransitionToAsync(_syncSessionContext, newStatus);
    }

    private Task OnBeatmapChanged(BeatmapIdentifier oldBeatmap, BeatmapIdentifier newBeatmap)
    {
        _stateMachine.Current?.OnBeatmapChanged(_syncSessionContext, newBeatmap);
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