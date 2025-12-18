using System.Diagnostics;
using System.Runtime.CompilerServices;
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

    private readonly SyncThrottler _musicThrottler;
    private readonly WaitCallback _musicSyncCallback;
    private SyncSessionContext _pendingMusicCtx;
    private int _pendingMusicMs;

    private readonly SyncThrottler _hitsoundThrottler;
    private readonly WaitCallback _hitsoundSyncCallback;
    private SyncSessionContext _pendingHitsoundCtx;
    private int _pendingHitsoundMs;

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

        _hitsoundSyncCallback = _ => HitsoundSyncWorkItem();
        _musicSyncCallback = _ => MusicSyncWorkItem();

        _musicThrottler = new SyncThrottler(MusicSyncIntervalTicks);
        _hitsoundThrottler = new SyncThrottler(HitsoundSyncIntervalTicks);

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
        _backgroundMusicManager.StartLowPass(200, 800);
        _backgroundMusicManager.SetResultFlag(false);

        if (ctx.Beatmap == default)
        {
            // Beatmap is required to start; keep silent if absent
            return;
        }

        await _gameplaySessionManager.StartAsync(ctx.Beatmap.FilenameFull, ctx.Beatmap.Filename);
    }

    public void Exit(SyncSessionContext ctx, OsuMemoryStatus to)
    {
        // Exit behavior will be handled by the next state's Enter.
    }

    public void OnPlayTimeChanged(SyncSessionContext ctx, int oldMs, int newMs, bool paused)
    {
        const int playingPauseThreshold = 5;
        var enableMixSync = _enableMixSync;
        if (enableMixSync)
        {
            _backgroundMusicManager.UpdatePauseCount(paused);
        }

        if (!ctx.IsStarted) return;

        // Retry: song time moved backward during playing
        if (oldMs > newMs)
        {
            OnRetry(ctx, enableMixSync);
            return;
        }

        var currentTimestamp = ctx.LastUpdateTimestamp;
        if (enableMixSync && _musicThrottler.TryAcquire(currentTimestamp))
        {
            _pendingMusicCtx = ctx;
            _pendingMusicMs = newMs;

            ThreadPool.UnsafeQueueUserWorkItem(_musicSyncCallback, null);
        }

        if (_hitsoundThrottler.TryAcquire(currentTimestamp))
        {
            _pendingHitsoundCtx = ctx;
            _pendingHitsoundMs = newMs;

            ThreadPool.UnsafeQueueUserWorkItem(_hitsoundSyncCallback, null);
        }
    }

    private void MusicSyncWorkItem()
    {
        try
        {
            SyncMusic(_pendingMusicCtx, _pendingMusicMs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing music.");
        }
        finally
        {
            _musicThrottler.Release();
        }
    }

    private void HitsoundSyncWorkItem()
    {
        try
        {
            SyncHitsounds(_pendingHitsoundCtx, _pendingHitsoundMs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing hitsounds.");
        }
        finally
        {
            _hitsoundThrottler.Release();
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
        if (_backgroundMusicManager.GetMainTrackPath() == null) return;
        if (_audioEngine.CurrentDevice == null) return;

        if (_backgroundMusicManager.GetPauseCount() >= playingPauseThreshold)
        {
            _backgroundMusicManager.ClearMainTrackAudio();
            return;
        }

        var musicPath = _backgroundMusicManager.GetMainTrackPath();
        if (musicPath == null) return;

        if (!_audioCacheManager.TryGet(musicPath, out var cachedAudio)) return;

        const int codeLatency = -1;
        const int osuForceLatency = 15;
        var oldMapForceOffset = _gameplaySessionManager.OsuFile.Version < 5 ? 24 : 0;
        _backgroundMusicManager.SetMainTrackOffsetAndLeadIn(osuForceLatency + codeLatency + oldMapForceOffset,
            _gameplaySessionManager.OsuFile.General.AudioLeadIn);
        if (!_backgroundMusicManager.IsResultFlag())
        {
            _backgroundMusicManager.SetSingleTrackPlayMods(ctx.PlayMods);
        }

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

    private sealed class SyncThrottler(long intervalTicks)
    {
        private long _lastTimestamp;
        private volatile int _isSyncing;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryAcquire(long currentTimestamp)
        {
            if (_isSyncing == 1) return false;

            var last = Interlocked.Read(ref _lastTimestamp);
            var elapsed = currentTimestamp - last;

            if (elapsed < intervalTicks) return false;
            if (Interlocked.CompareExchange(ref _lastTimestamp, currentTimestamp, last) != last) return false;

            _isSyncing = 1;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Release()
        {
            _isSyncing = 0;
        }
    }
}