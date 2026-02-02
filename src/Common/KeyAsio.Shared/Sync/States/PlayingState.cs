using System.Diagnostics;
using Coosu.Beatmap.Sections.GamePlay;
using KeyAsio.Core.Audio;
using KeyAsio.Plugins.Abstractions.OsuMemory;
using KeyAsio.Shared.Hitsounds.Playback;
using KeyAsio.Shared.Models;
using KeyAsio.Shared.OsuMemory;
using KeyAsio.Shared.Sync.Services;
using Microsoft.Extensions.Logging;

namespace KeyAsio.Shared.Sync.States;

public class PlayingState : IGameState
{
    private static readonly long s_hitsoundSyncIntervalTicks = Stopwatch.Frequency / 1000; // 1000hz

    private readonly ILogger<PlayingState> _logger;
    private readonly IPlaybackEngine _playbackEngine;
    private readonly BeatmapHitsoundLoader _beatmapHitsoundLoader;
    private readonly SfxPlaybackService _sfxPlaybackService;
    private readonly SharedViewModel _sharedViewModel;
    private readonly GameplaySessionManager _gameplaySessionManager;
    private readonly GameplayAudioService _gameplayAudioService;
    private readonly List<PlaybackInfo> _playbackBuffer = new(64);

    private long _lastHitsoundSyncTimestamp;
    private bool _disableComboBreakSfx;
    private bool? _lastIsReplay;

    public PlayingState(
        ILogger<PlayingState> logger,
        AppSettings appSettings,
        IPlaybackEngine playbackEngine,
        BeatmapHitsoundLoader beatmapHitsoundLoader,
        SfxPlaybackService sfxPlaybackService,
        SharedViewModel sharedViewModel,
        GameplaySessionManager gameplaySessionManager,
        GameplayAudioService gameplayAudioService)
    {
        _logger = logger;
        _playbackEngine = playbackEngine;
        _beatmapHitsoundLoader = beatmapHitsoundLoader;
        _sfxPlaybackService = sfxPlaybackService;
        _sharedViewModel = sharedViewModel;
        _gameplaySessionManager = gameplaySessionManager;
        _gameplayAudioService = gameplayAudioService;

        _disableComboBreakSfx = appSettings.Sync.Filters.DisableComboBreakSfx;
        appSettings.Sync.Filters.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppSettings.Sync.Filters.DisableComboBreakSfx))
            {
                _disableComboBreakSfx = appSettings.Sync.Filters.DisableComboBreakSfx;
            }
        };
    }

    public async Task EnterAsync(SyncSessionContext ctx, OsuMemoryStatus from)
    {
        _lastHitsoundSyncTimestamp = 0;
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
        if (!ctx.IsStarted) return;

        if (_lastIsReplay != ctx.IsReplay)
        {
            _logger.LogInformation("IsReplay status changed: {Old} -> {New} (Mods: {Mods})", _lastIsReplay,
                ctx.IsReplay, ctx.PlayMods);
            _lastIsReplay = ctx.IsReplay;
        }

        // Retry: song time moved backward during playing
        if (prevMs > currMs)
        {
            OnRetry(ctx);
            return;
        }

        var timestamp = ctx.LastUpdateTimestamp;

        // Logic for Hitsounds
        if (timestamp - _lastHitsoundSyncTimestamp >= s_hitsoundSyncIntervalTicks)
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

    private void OnRetry(SyncSessionContext ctx)
    {
        var mixer = _playbackEngine.EffectMixer;
        _sfxPlaybackService.ClearAllLoops(mixer);
        mixer?.RemoveAllMixerInputs();
        _beatmapHitsoundLoader.ResetNodes(_gameplaySessionManager.CurrentHitsoundSequencer, ctx.PlayTime);
    }

    private void SyncHitsounds(SyncSessionContext ctx, int newMs)
    {
        _beatmapHitsoundLoader.AdvanceCachingWindow(newMs);
        PlayAutoPlaybackIfNeeded(ctx);
        PlayManualPlaybackIfNeeded(ctx);
    }

    private void PlayAutoPlaybackIfNeeded(SyncSessionContext ctx)
    {
        if (!_sharedViewModel.AutoMode &&
            (ctx.PlayMods & Mods.Autoplay) == 0 &&
            (ctx.PlayMods & Mods.Relax) == 0 &&
            !ctx.IsReplay) return;

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
                playbackObject.PlaybackEvent is SampleEvent { Layer: SampleLayer.Sampling })
            {
                _sfxPlaybackService.DispatchPlayback(playbackObject, playbackObject.PlaybackEvent.Volume * 0.866666666f);
                continue;
            }

            _sfxPlaybackService.DispatchPlayback(playbackObject);
        }
    }
}