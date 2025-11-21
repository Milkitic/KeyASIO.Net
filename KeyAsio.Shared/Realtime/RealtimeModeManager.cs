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
using OsuMemoryDataProvider;

namespace KeyAsio.Shared.Realtime;

public class RealtimeModeManager
{
    private readonly RealtimeProperties _realtimeProperties;
    private readonly RealtimeStateMachine _stateMachine;

    public RealtimeModeManager(IServiceProvider serviceProvider,
        AppSettings appSettings,
        AudioEngine audioEngine,
        SharedViewModel sharedViewModel,
        AudioCacheService audioCacheService,
        HitsoundNodeService hitsoundNodeService,
        MusicTrackService musicTrackService,
        AudioPlaybackService audioPlaybackService,
        AudioCacheManager audioCacheManager,
        PlaySessionManager playSessionManager,
        RealtimeProperties realtimeProperties)
    {
        _realtimeProperties = realtimeProperties;
        _realtimeProperties.OnBeatmapChanged = OnBeatmapChanged;
        _realtimeProperties.OnComboChanged = OnComboChanged;
        _realtimeProperties.OnStatusChanged = OnStatusChanged;
        _realtimeProperties.OnPlayModsChanged = OnPlayModsChanged;
        _realtimeProperties.OnFetchedPlayTimeChanged = OnFetchedPlayTimeChanged;

        var standardAudioProvider = new StandardAudioProvider(
            serviceProvider.GetRequiredService<ILogger<StandardAudioProvider>>(),
            appSettings, realtimeProperties, audioEngine, audioCacheService, playSessionManager);
        var maniaAudioProvider = new ManiaAudioProvider(
            serviceProvider.GetRequiredService<ILogger<ManiaAudioProvider>>(),
            appSettings, realtimeProperties, audioEngine, audioCacheService, playSessionManager);
        playSessionManager.InitializeProviders(standardAudioProvider, maniaAudioProvider);

        // Initialize realtime state machine with scene mappings
        _stateMachine = new RealtimeStateMachine(new Dictionary<OsuMemoryStatus, IRealtimeState>
        {
            [OsuMemoryStatus.Playing] = new PlayingState(appSettings, audioEngine, audioCacheManager, musicTrackService,
                hitsoundNodeService, audioPlaybackService, sharedViewModel, playSessionManager, audioCacheService),
            [OsuMemoryStatus.ResultsScreen] = new ResultsState(musicTrackService),
            [OsuMemoryStatus.NotRunning] = new NotRunningState(appSettings, musicTrackService),
            [OsuMemoryStatus.SongSelect] = new BrowsingState(appSettings, musicTrackService, playSessionManager),
            [OsuMemoryStatus.SongSelectEdit] = new BrowsingState(appSettings, musicTrackService, playSessionManager),
            [OsuMemoryStatus.MainMenu] = new BrowsingState(appSettings, musicTrackService, playSessionManager),
            [OsuMemoryStatus.MultiplayerSongSelect] =
                new BrowsingState(appSettings, musicTrackService, playSessionManager),
        });
        Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.AboveNormal;
    }


    private async ValueTask OnComboChanged(int oldCombo, int newCombo)
    {
        _stateMachine.Current?.OnComboChanged(_realtimeProperties, oldCombo, newCombo);
    }

    private async ValueTask OnStatusChanged(OsuMemoryStatus oldStatus, OsuMemoryStatus newStatus)
    {
        await _stateMachine.TransitionToAsync(_realtimeProperties, newStatus);
    }

    private async ValueTask OnBeatmapChanged(BeatmapIdentifier oldBeatmap, BeatmapIdentifier newBeatmap)
    {
        _stateMachine.Current?.OnBeatmapChanged(_realtimeProperties, newBeatmap);
    }

    private async ValueTask OnPlayModsChanged(Mods oldMods, Mods newMods)
    {
        _stateMachine.Current?.OnModsChanged(_realtimeProperties, oldMods, newMods);
    }

    private async ValueTask OnFetchedPlayTimeChanged(int oldMs, int newMs, bool paused = false)
    {
        _stateMachine.Current?.OnPlayTimeChanged(_realtimeProperties, oldMs, newMs, paused);
    }
}