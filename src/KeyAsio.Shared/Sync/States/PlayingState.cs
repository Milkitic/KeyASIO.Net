using Coosu.Beatmap.Extensions.Playback;
using Coosu.Beatmap.Sections.GamePlay;
using KeyAsio.Audio;
using KeyAsio.Audio.Caching;
using KeyAsio.Shared.Models;
using KeyAsio.Shared.OsuMemory;
using KeyAsio.Shared.Sync.Services;

namespace KeyAsio.Shared.Sync.States;

public class PlayingState : IGameState
{
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

    public PlayingState(AppSettings appSettings,
        AudioEngine audioEngine,
        AudioCacheManager audioCacheManager,
        BackgroundMusicManager backgroundMusicManager,
        BeatmapHitsoundLoader beatmapHitsoundLoader,
        SfxPlaybackService sfxPlaybackService,
        SharedViewModel sharedViewModel,
        GameplaySessionManager gameplaySessionManager,
        GameplayAudioService gameplayAudioService)
    {
        _appSettings = appSettings;
        _audioEngine = audioEngine;
        _audioCacheManager = audioCacheManager;
        _backgroundMusicManager = backgroundMusicManager;
        _beatmapHitsoundLoader = beatmapHitsoundLoader;
        _sfxPlaybackService = sfxPlaybackService;
        _sharedViewModel = sharedViewModel;
        _gameplaySessionManager = gameplaySessionManager;
        _gameplayAudioService = gameplayAudioService;
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

    public async Task OnPlayTimeChanged(SyncSessionContext ctx, int oldMs, int newMs, bool paused)
    {
        const int playingPauseThreshold = 5;
        _backgroundMusicManager.UpdatePauseCount(paused);

        if (!ctx.IsStarted) return;

        // Retry: song time moved backward during playing
        if (oldMs > newMs)
        {
            _backgroundMusicManager.SetPauseCount(0);
            _backgroundMusicManager.StopCurrentMusic();
            _backgroundMusicManager.StartLowPass(200, 16000);
            _backgroundMusicManager.SetFirstStartInitialized(true);
            var mixer = _audioEngine.EffectMixer;
            _sfxPlaybackService.ClearAllLoops(mixer);
            _backgroundMusicManager.ClearMainTrackAudio();
            mixer?.RemoveAllMixerInputs();
            _beatmapHitsoundLoader.ResetNodes(_gameplaySessionManager.CurrentHitsoundSequencer, ctx.PlayTime);
            return;
        }

        if (_appSettings.Sync.EnableMixSync)
        {
            if (_backgroundMusicManager.GetFirstStartInitialized() && _gameplaySessionManager.OsuFile != null &&
                _backgroundMusicManager.GetMainTrackPath() != null &&
                _audioEngine.CurrentDevice != null)
            {
                if (_backgroundMusicManager.GetPauseCount() >= playingPauseThreshold)
                {
                    _backgroundMusicManager.ClearMainTrackAudio();
                }
                else
                {
                    var musicPath = _backgroundMusicManager.GetMainTrackPath();
                    if (musicPath != null)
                    {
                        var cachedAudio = await _audioCacheManager.TryGetAsync(musicPath);
                        if (cachedAudio != null)
                        {
                            const int codeLatency = -1;
                            const int osuForceLatency = 15;
                            var oldMapForceOffset = _gameplaySessionManager.OsuFile.Version < 5 ? 24 : 0;
                            _backgroundMusicManager.SetMainTrackOffsetAndLeadIn(
                                osuForceLatency + codeLatency + oldMapForceOffset,
                                _gameplaySessionManager.OsuFile.General.AudioLeadIn);
                            if (!_backgroundMusicManager.IsResultFlag())
                            {
                                _backgroundMusicManager.SetSingleTrackPlayMods(ctx.PlayMods);
                            }

                            _backgroundMusicManager.SyncMainTrackAudio(cachedAudio, newMs);
                        }
                    }
                }
            }
        }

        _beatmapHitsoundLoader.AdvanceCachingWindow(newMs);
        PlayAutoPlaybackIfNeeded(ctx);
        PlayManualPlaybackIfNeeded(ctx);
    }

    public void OnComboChanged(SyncSessionContext ctx, int oldCombo, int newCombo)
    {
        if (_appSettings.Sync.Filters.DisableComboBreakSfx) return;
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

    private void PlayAutoPlaybackIfNeeded(SyncSessionContext ctx)
    {
        if (!_sharedViewModel.AutoMode && (ctx.PlayMods & Mods.Autoplay) == 0 && !ctx.IsReplay) return;
        _playbackBuffer.Clear();
        _gameplaySessionManager.CurrentHitsoundSequencer.ProcessAutoPlay(_playbackBuffer, false);
        foreach (var playbackObject in _playbackBuffer)
        {
            _sfxPlaybackService.DispatchPlayback(playbackObject);
        }
    }

    private void PlayManualPlaybackIfNeeded(SyncSessionContext ctx)
    {
        _playbackBuffer.Clear();
        _gameplaySessionManager.CurrentHitsoundSequencer.ProcessAutoPlay(_playbackBuffer, true);
        foreach (var playbackObject in _playbackBuffer)
        {
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