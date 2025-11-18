using KeyAsio.Audio;
using KeyAsio.Audio.Caching;
using KeyAsio.MemoryReading;
using OsuMemoryDataProvider;

namespace KeyAsio.Shared.Realtime.States;

public class PlayingState : IRealtimeState
{
    private readonly AudioEngine _audioEngine;
    private readonly AudioCacheManager _audioCacheManager;

    public PlayingState(AudioEngine audioEngine, AudioCacheManager audioCacheManager)
    {
        _audioEngine = audioEngine;
        _audioCacheManager = audioCacheManager;
    }

    public async Task EnterAsync(RealtimeModeManager ctx, OsuMemoryStatus from)
    {
        ctx.StartLowPass(200, 800);
        ctx.SetResultFlag(false);

        if (ctx.Beatmap == default)
        {
            // Beatmap is required to start; keep silent if absent
            return;
        }

        await ctx.StartAsync(ctx.Beatmap.FilenameFull, ctx.Beatmap.Filename);
    }

    public void Exit(RealtimeModeManager ctx, OsuMemoryStatus to)
    {
        // Exit behavior will be handled by the next state's Enter.
    }

    public async Task OnPlayTimeChanged(RealtimeModeManager ctx, int oldMs, int newMs, bool paused)
    {
        const int playingPauseThreshold = 5;
        ctx.UpdatePauseCount(paused);

        if (!ctx.IsStarted) return;

        // Retry: song time moved backward during playing
        if (oldMs > newMs)
        {
            ctx.SetPauseCount(0);
            ctx.StopCurrentMusic();
            ctx.StartLowPass(200, 16000);
            ctx.SetFirstStartInitialized(true);
            ctx.ClearMixerLoopsAndMainTrackAudio();
            ctx.ResetNodesExternal();
            return;
        }

        if (ctx.GetEnableMusicFunctions())
        {
            if (ctx.GetFirstStartInitialized() && ctx.OsuFile != null && ctx.GetMusicPath() != null &&
                _audioEngine.CurrentDevice != null)
            {
                if (ctx.GetPauseCount() >= playingPauseThreshold)
                {
                    ctx.ClearMainTrackAudio();
                }
                else
                {
                    var musicPath = ctx.GetMusicPath();
                    if (musicPath != null)
                    {
                        var cachedAudio = await _audioCacheManager.TryGetAsync(musicPath);
                        if (cachedAudio != null)
                        {
                            const int codeLatency = -1;
                            const int osuForceLatency = 15;
                            var oldMapForceOffset = ctx.OsuFile.Version < 5 ? 24 : 0;
                            ctx.SetMainTrackOffsetAndLeadIn(osuForceLatency + codeLatency + oldMapForceOffset,
                                ctx.OsuFile.General.AudioLeadIn);
                            if (!ctx.IsResultFlag())
                            {
                                ctx.SetSingleTrackPlayMods(ctx.PlayMods);
                            }

                            ctx.SyncMainTrackAudio(cachedAudio, newMs);
                        }
                    }
                }
            }
        }

        ctx.AdvanceCachingWindow(newMs);
        ctx.PlayAutoPlaybackIfNeeded();
        ctx.PlayManualPlaybackIfNeeded();
    }

    public void OnComboChanged(RealtimeModeManager ctx, int oldCombo, int newCombo)
    {
        if (ctx.AppSettings.RealtimeOptions.IgnoreComboBreak) return;
        if (!ctx.IsStarted) return;
        if (ctx.Score == 0) return;
        if (newCombo >= oldCombo || oldCombo < 20) return;

        if (ctx.TryGetCachedAudio("combobreak", out var cachedAudio))
        {
            ctx.PlayAudio(cachedAudio, 1, 0);
        }
    }

    public void OnBeatmapChanged(RealtimeModeManager ctx, BeatmapIdentifier beatmap)
    {
    }

    public void OnModsChanged(RealtimeModeManager ctx, Mods oldMods, Mods newMods)
    {
    }
}