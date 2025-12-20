using System.Diagnostics;
using Coosu.Beatmap.Extensions.Playback;
using Coosu.Beatmap.Sections.GamePlay;
using KeyAsio.Audio;
using KeyAsio.Audio.Caching;
using KeyAsio.Shared.Models;
using KeyAsio.Shared.OsuMemory;
using KeyAsio.Shared.Sync.Services;
using Microsoft.Extensions.Logging;

namespace KeyAsio.Shared.Sync.States;

public class PlayingState : IGameState
{
    private static readonly long HitsoundSyncIntervalTicks = Stopwatch.Frequency / 1000; // 1000hz
    private static readonly long MusicSyncIntervalTicks = Stopwatch.Frequency / 1000;

    private readonly ILogger<PlayingState> _logger;
    private readonly AppSettings _appSettings;
    private readonly AudioEngine _audioEngine;
    private readonly AudioCacheManager _audioCacheManager;
    private readonly BackgroundMusicManager _backgroundMusicManager;
    private readonly BeatmapHitsoundLoader _beatmapHitsoundLoader;
    private readonly SfxPlaybackService _sfxPlaybackService;
    private readonly SharedViewModel _sharedViewModel;
    private readonly GameplaySessionManager _gameplaySessionManager;
    private readonly GameplayAudioService _gameplayAudioService;
    private readonly List<PlaybackInfo> _playbackBuffer = new(64);

    private long _lastMusicSyncTimestamp;
    private long _lastHitsoundSyncTimestamp;

    private bool _enableMixSync;
    private bool _disableComboBreakSfx;

    public PlayingState(
        ILogger<PlayingState> logger,
        AppSettings appSettings,
        AudioEngine audioEngine,
        AudioCacheManager audioCacheManager,
        BackgroundMusicManager backgroundMusicManager,
        BeatmapHitsoundLoader beatmapHitsoundLoader,
        SfxPlaybackService sfxPlaybackService,
        SharedViewModel sharedViewModel,
        GameplaySessionManager gameplaySessionManager,
        GameplayAudioService gameplayAudioService)
    {
        _logger = logger;
        _appSettings = appSettings;
        _audioEngine = audioEngine;
        _audioCacheManager = audioCacheManager;
        _backgroundMusicManager = backgroundMusicManager;
        _beatmapHitsoundLoader = beatmapHitsoundLoader;
        _sfxPlaybackService = sfxPlaybackService;
        _sharedViewModel = sharedViewModel;
        _gameplaySessionManager = gameplaySessionManager;
        _gameplayAudioService = gameplayAudioService;

        _enableMixSync = _appSettings.Sync.EnableMixSync;
        _disableComboBreakSfx = _appSettings.Sync.Filters.DisableComboBreakSfx;
        _appSettings.Sync.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppSettings.Sync.EnableMixSync))
            {
                _enableMixSync = _appSettings.Sync.EnableMixSync;
            }
        };
        _appSettings.Sync.Filters.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppSettings.Sync.Filters.DisableComboBreakSfx))
            {
                _disableComboBreakSfx = _appSettings.Sync.Filters.DisableComboBreakSfx;
            }
        };
    }

    public async Task EnterAsync(SyncSessionContext ctx, OsuMemoryStatus from)
    {
        _lastMusicSyncTimestamp = 0;
        _lastHitsoundSyncTimestamp = 0;

        _backgroundMusicManager.StartLowPass(200, 800);

        if (ctx.Beatmap == default)
        {
            // Beatmap is required to start; keep silent if absent
            return;
        }

        await _gameplaySessionManager.StartAsync(ctx.Beatmap.FilenameFull, ctx.Beatmap.Filename);
    }

    public void Exit(SyncSessionContext ctx, OsuMemoryStatus to)
    {
    }

    public void OnTick(SyncSessionContext ctx, int prevMs, int currMs, bool isPaused)
    {
        var enableMixSync = _enableMixSync;
        if (enableMixSync)
        {
            _backgroundMusicManager.UpdatePauseCount(isPaused);
        }

        if (!ctx.IsStarted) return;

        // Retry: song time moved backward during playing
        if (prevMs > currMs)
        {
            OnRetry(ctx, enableMixSync);
            return;
        }

        var timestamp = ctx.LastUpdateTimestamp;

        // Logic for Music
        if (timestamp - _lastMusicSyncTimestamp >= MusicSyncIntervalTicks)
        {
            if (_enableMixSync)
            {
                try
                {
                    SyncMusic(ctx, currMs);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error syncing music.");
                }

                _lastMusicSyncTimestamp = timestamp;
            }
        }

        // Logic for Hitsounds
        if (timestamp - _lastHitsoundSyncTimestamp >= HitsoundSyncIntervalTicks)
        {
            try
            {
                SyncHitsounds(ctx, currMs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing hitsounds.");
            }

            _lastHitsoundSyncTimestamp = timestamp;
        }
    }

    public void OnComboChanged(SyncSessionContext ctx, int oldCombo, int newCombo)
    {
        if (_disableComboBreakSfx) return;
        if (!ctx.IsStarted) return;
        if (ctx.Score == 0) return;
        if (newCombo >= oldCombo || oldCombo < 20) return;

        if (_gameplayAudioService.TryGetCachedAudio("combobreak", out var cachedAudio))
        {
            _sfxPlaybackService.PlayEffectsAudio(cachedAudio, 1, 0);
        }
    }

    public void OnBeatmapChanged(SyncSessionContext ctx, BeatmapIdentifier beatmap)
    {
    }

    public void OnModsChanged(SyncSessionContext ctx, Mods oldMods, Mods newMods)
    {
    }

    private void OnRetry(SyncSessionContext ctx, bool enableMixSync)
    {
        if (enableMixSync)
        {
            _backgroundMusicManager.SetPauseCount(0);
            _backgroundMusicManager.StopCurrentMusic();
            _backgroundMusicManager.StartLowPass(200, 16000);
            _backgroundMusicManager.SetFirstStartInitialized(true);
        }

        var mixer = _audioEngine.EffectMixer;
        _sfxPlaybackService.ClearAllLoops(mixer);
        if (enableMixSync)
        {
            _backgroundMusicManager.ClearMainTrackAudio();
        }

        mixer?.RemoveAllMixerInputs();
        _beatmapHitsoundLoader.ResetNodes(_gameplaySessionManager.CurrentHitsoundSequencer, ctx.PlayTime);
    }

    private void SyncMusic(SyncSessionContext ctx, int newMs)
    {
        const int playingPauseThreshold = 5;
        if (!_backgroundMusicManager.GetFirstStartInitialized()) return;
        if (_gameplaySessionManager.OsuFile == null) return;

        var folder = _gameplaySessionManager.BeatmapFolder;
        var filename = _gameplaySessionManager.AudioFilename;

        if (folder == null || filename == null) return;
        if (_audioEngine.CurrentDevice == null) return;

        if (_backgroundMusicManager.GetPauseCount() >= playingPauseThreshold)
        {
            _backgroundMusicManager.ClearMainTrackAudio();
            return;
        }

        var musicPath = Path.Combine(folder, filename);
        if (!_audioCacheManager.TryGet(musicPath, out var cachedAudio)) return;

        const int codeLatency = -1;
        const int osuForceLatency = 15;
        var oldMapForceOffset = _gameplaySessionManager.OsuFile.Version < 5 ? 24 : 0;
        _backgroundMusicManager.SetMainTrackOffsetAndLeadIn(osuForceLatency + codeLatency + oldMapForceOffset,
            _gameplaySessionManager.OsuFile.General.AudioLeadIn);

        _backgroundMusicManager.SetSingleTrackPlayMods(ctx.PlayMods);

        _backgroundMusicManager.SyncMainTrackAudio(cachedAudio, newMs);
    }

    private void SyncHitsounds(SyncSessionContext ctx, int newMs)
    {
        _beatmapHitsoundLoader.AdvanceCachingWindow(newMs);
        PlayAutoPlaybackIfNeeded(ctx);
        PlayManualPlaybackIfNeeded(ctx);
    }

    private void PlayAutoPlaybackIfNeeded(SyncSessionContext ctx)
    {
        if (!_sharedViewModel.AutoMode && (ctx.PlayMods & Mods.Autoplay) == 0 && !ctx.IsReplay) return;
        _playbackBuffer.Clear();
        _gameplaySessionManager.CurrentHitsoundSequencer.ProcessAutoPlay(_playbackBuffer, false);

        var count = _playbackBuffer.Count;
        for (int i = 0; i < count; i++)
        {
            var playbackObject = _playbackBuffer[i];
            _sfxPlaybackService.DispatchPlayback(playbackObject);
        }
    }

    private void PlayManualPlaybackIfNeeded(SyncSessionContext ctx)
    {
        _playbackBuffer.Clear();
        _gameplaySessionManager.CurrentHitsoundSequencer.ProcessAutoPlay(_playbackBuffer, true);

        var count = _playbackBuffer.Count;
        for (int i = 0; i < count; i++)
        {
            var playbackObject = _playbackBuffer[i];
            if (_gameplaySessionManager.OsuFile.General.Mode == GameMode.Mania &&
                playbackObject.HitsoundNode is PlayableNode { PlayablePriority: PlayablePriority.Sampling })
            {
                _sfxPlaybackService.DispatchPlayback(playbackObject, playbackObject.HitsoundNode.Volume * 0.6666666f);
                continue;
            }

            _sfxPlaybackService.DispatchPlayback(playbackObject);
        }
    }
}