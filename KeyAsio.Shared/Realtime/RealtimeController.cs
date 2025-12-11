using System.Diagnostics;
using KeyAsio.Audio;
using KeyAsio.Audio.Caching;
using KeyAsio.MemoryReading;
using KeyAsio.Shared.Models;
using KeyAsio.Shared.Realtime.AudioProviders;
using KeyAsio.Shared.Realtime.Services;
using KeyAsio.Shared.Realtime.States;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using KeyAsio.MemoryReading.OsuMemoryModels;

namespace KeyAsio.Shared.Realtime;

public class RealtimeController
{
    private readonly RealtimeSessionContext _realtimeSessionContext;
    private readonly GameStateMachine _stateMachine;

    public RealtimeController(IServiceProvider serviceProvider,
        AppSettings appSettings,
        AudioEngine audioEngine,
        SharedViewModel sharedViewModel,
        GameplayAudioService gameplayAudioService,
        BeatmapHitsoundLoader beatmapHitsoundLoader,
        BackgroundMusicManager backgroundMusicManager,
        SfxPlaybackService sfxPlaybackService,
        AudioCacheManager audioCacheManager,
        GameplaySessionManager gameplaySessionManager,
        RealtimeSessionContext realtimeSessionContext)
    {
        _realtimeSessionContext = realtimeSessionContext;
        _realtimeSessionContext.OnBeatmapChanged = OnBeatmapChanged;
        _realtimeSessionContext.OnComboChanged = OnComboChanged;
        _realtimeSessionContext.OnStatusChanged = OnStatusChanged;
        _realtimeSessionContext.OnPlayModsChanged = OnPlayModsChanged;
        _realtimeSessionContext.OnFetchedPlayTimeChanged = OnFetchedPlayTimeChanged;

        var standardAudioProvider = new StandardHitsoundSequencer(
            serviceProvider.GetRequiredService<ILogger<StandardHitsoundSequencer>>(),
            appSettings, realtimeSessionContext, audioEngine, gameplayAudioService, gameplaySessionManager);
        var maniaAudioProvider = new ManiaHitsoundSequencer(
            serviceProvider.GetRequiredService<ILogger<ManiaHitsoundSequencer>>(),
            appSettings, realtimeSessionContext, audioEngine, gameplayAudioService, gameplaySessionManager);
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
        Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.AboveNormal;
    }


    private async ValueTask OnComboChanged(int oldCombo, int newCombo)
    {
        _stateMachine.Current?.OnComboChanged(_realtimeSessionContext, oldCombo, newCombo);
    }

    private async ValueTask OnStatusChanged(OsuMemoryStatus oldStatus, OsuMemoryStatus newStatus)
    {
        await _stateMachine.TransitionToAsync(_realtimeSessionContext, newStatus);
    }

    private async ValueTask OnBeatmapChanged(BeatmapIdentifier oldBeatmap, BeatmapIdentifier newBeatmap)
    {
        _stateMachine.Current?.OnBeatmapChanged(_realtimeSessionContext, newBeatmap);
    }

    private async ValueTask OnPlayModsChanged(Mods oldMods, Mods newMods)
    {
        _stateMachine.Current?.OnModsChanged(_realtimeSessionContext, oldMods, newMods);
    }

    private async ValueTask OnFetchedPlayTimeChanged(int oldMs, int newMs, bool paused = false)
    {
        _stateMachine.Current?.OnPlayTimeChanged(_realtimeSessionContext, oldMs, newMs, paused);
    }
}