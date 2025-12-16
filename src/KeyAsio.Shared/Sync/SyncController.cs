using KeyAsio.Audio;
using KeyAsio.Audio.Caching;
using KeyAsio.Shared.Models;
using KeyAsio.Shared.OsuMemory;
using KeyAsio.Shared.Sync.AudioProviders;
using KeyAsio.Shared.Sync.Services;
using KeyAsio.Shared.Sync.States;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KeyAsio.Shared.Sync;

public class SyncController
{
    private readonly SyncSessionContext _syncSessionContext;
    private readonly GameStateMachine _stateMachine;

    public SyncController(IServiceProvider serviceProvider,
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
        _syncSessionContext.OnFetchedPlayTimeChanged = OnFetchedPlayTimeChanged;

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
            [OsuMemoryStatus.Playing] = new PlayingState(appSettings, audioEngine, audioCacheManager, backgroundMusicManager,
                beatmapHitsoundLoader, sfxPlaybackService, sharedViewModel, gameplaySessionManager, gameplayAudioService),
            [OsuMemoryStatus.ResultsScreen] = new ResultsState(backgroundMusicManager),
            [OsuMemoryStatus.NotRunning] = new NotRunningState(appSettings, backgroundMusicManager),
            [OsuMemoryStatus.SongSelection] = new BrowsingState(appSettings, backgroundMusicManager, gameplaySessionManager),
            [OsuMemoryStatus.EditSongSelection] = new BrowsingState(appSettings, backgroundMusicManager, gameplaySessionManager),
            [OsuMemoryStatus.MainView] = new BrowsingState(appSettings, backgroundMusicManager, gameplaySessionManager),
            [OsuMemoryStatus.MultiSongSelection] =
                new BrowsingState(appSettings, backgroundMusicManager, gameplaySessionManager),
        });
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

    private Task OnFetchedPlayTimeChanged(int oldMs, int newMs, bool paused = false)
    {
        _stateMachine.Current?.OnPlayTimeChanged(_syncSessionContext, oldMs, newMs, paused);
        return Task.CompletedTask;
    }
}