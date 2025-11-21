using KeyAsio.Audio;
using KeyAsio.Audio.Caching;
using KeyAsio.MemoryReading;
using KeyAsio.Shared.Models;
using KeyAsio.Shared.Realtime.Services;
using OsuMemoryDataProvider;

namespace KeyAsio.Shared.Realtime.States;

public class PlayingState : IRealtimeState
{
    private readonly AppSettings _appSettings;
    private readonly AudioEngine _audioEngine;
    private readonly AudioCacheManager _audioCacheManager;
    private readonly MusicTrackService _musicTrackService;
    private readonly HitsoundNodeService _hitsoundNodeService;
    private readonly AudioPlaybackService _audioPlaybackService;
    private readonly SharedViewModel _sharedViewModel;
    private readonly AudioCacheService _audioCacheService;
    private readonly List<PlaybackInfo> _playbackBuffer = new(64);

    public PlayingState(AppSettings appSettings,
        AudioEngine audioEngine,
        AudioCacheManager audioCacheManager,
        MusicTrackService musicTrackService,
        HitsoundNodeService hitsoundNodeService,
        AudioPlaybackService audioPlaybackService,
        SharedViewModel sharedViewModel,
        AudioCacheService audioCacheService)
    {
        _appSettings = appSettings;
        _audioEngine = audioEngine;
        _audioCacheManager = audioCacheManager;
        _musicTrackService = musicTrackService;
        _hitsoundNodeService = hitsoundNodeService;
        _audioPlaybackService = audioPlaybackService;
        _sharedViewModel = sharedViewModel;
        _audioCacheService = audioCacheService;
    }

    public async Task EnterAsync(IRealtimeContext ctx, OsuMemoryStatus from)
    {
        _musicTrackService.StartLowPass(200, 800);
        _musicTrackService.SetResultFlag(false);

        if (ctx.Beatmap == default)
        {
            // Beatmap is required to start; keep silent if absent
            return;
        }

        await ctx.StartAsync(ctx.Beatmap.FilenameFull, ctx.Beatmap.Filename);
    }

    public void Exit(IRealtimeContext ctx, OsuMemoryStatus to)
    {
        // Exit behavior will be handled by the next state's Enter.
    }

    public async Task OnPlayTimeChanged(IRealtimeContext ctx, int oldMs, int newMs, bool paused)
    {
        const int playingPauseThreshold = 5;
        _musicTrackService.UpdatePauseCount(paused);

        if (!ctx.IsStarted) return;

        // Retry: song time moved backward during playing
        if (oldMs > newMs)
        {
            _musicTrackService.SetPauseCount(0);
            _musicTrackService.StopCurrentMusic();
            _musicTrackService.StartLowPass(200, 16000);
            _musicTrackService.SetFirstStartInitialized(true);
            var mixer = _audioEngine.EffectMixer;
            _audioPlaybackService.ClearAllLoops(mixer);
            _musicTrackService.ClearMainTrackAudio();
            mixer?.RemoveAllMixerInputs();
            _hitsoundNodeService.ResetNodes(ctx.CurrentAudioProvider, ctx.PlayTime);
            return;
        }

        if (_appSettings.RealtimeOptions.EnableMusicFunctions)
        {
            if (_musicTrackService.GetFirstStartInitialized() && ctx.OsuFile != null &&
                _musicTrackService.GetMainTrackPath() != null &&
                _audioEngine.CurrentDevice != null)
            {
                if (_musicTrackService.GetPauseCount() >= playingPauseThreshold)
                {
                    _musicTrackService.ClearMainTrackAudio();
                }
                else
                {
                    var musicPath = _musicTrackService.GetMainTrackPath();
                    if (musicPath != null)
                    {
                        var cachedAudio = await _audioCacheManager.TryGetAsync(musicPath);
                        if (cachedAudio != null)
                        {
                            const int codeLatency = -1;
                            const int osuForceLatency = 15;
                            var oldMapForceOffset = ctx.OsuFile.Version < 5 ? 24 : 0;
                            _musicTrackService.SetMainTrackOffsetAndLeadIn(
                                osuForceLatency + codeLatency + oldMapForceOffset,
                                ctx.OsuFile.General.AudioLeadIn);
                            if (!_musicTrackService.IsResultFlag())
                            {
                                _musicTrackService.SetSingleTrackPlayMods(ctx.PlayMods);
                            }

                            _musicTrackService.SyncMainTrackAudio(cachedAudio, newMs);
                        }
                    }
                }
            }
        }

        _hitsoundNodeService.AdvanceCachingWindow(newMs);
        PlayAutoPlaybackIfNeeded(ctx);
        PlayManualPlaybackIfNeeded(ctx);
    }

    public void OnComboChanged(IRealtimeContext ctx, int oldCombo, int newCombo)
    {
        if (_appSettings.RealtimeOptions.IgnoreComboBreak) return;
        if (!ctx.IsStarted) return;
        if (ctx.Score == 0) return;
        if (newCombo >= oldCombo || oldCombo < 20) return;

        if (_audioCacheService.TryGetCachedAudio("combobreak", out var cachedAudio))
        {
            _audioPlaybackService.PlayEffectsAudio(cachedAudio, 1, 0);
        }
    }

    public void OnBeatmapChanged(IRealtimeContext ctx, BeatmapIdentifier beatmap)
    {
    }

    public void OnModsChanged(IRealtimeContext ctx, Mods oldMods, Mods newMods)
    {
    }

    private void PlayAutoPlaybackIfNeeded(IRealtimeContext ctx)
    {
        if (!_sharedViewModel.AutoMode && (ctx.PlayMods & Mods.Autoplay) == 0 && !ctx.IsReplay) return;
        _playbackBuffer.Clear();
        ctx.FillPlaybackAudio(_playbackBuffer, false);
        foreach (var playbackObject in _playbackBuffer)
        {
            _audioPlaybackService.DispatchPlayback(playbackObject);
        }
    }

    private void PlayManualPlaybackIfNeeded(IRealtimeContext ctx)
    {
        _playbackBuffer.Clear();
        ctx.FillPlaybackAudio(_playbackBuffer, true);
        foreach (var playbackObject in _playbackBuffer)
        {
            _audioPlaybackService.DispatchPlayback(playbackObject);
        }
    }
}