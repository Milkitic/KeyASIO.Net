using System;
using KeyAsio.Gui.Models;
using KeyAsio.Gui.Utils;
using KeyAsio.Gui.Waves;
using Microsoft.Extensions.Logging;
using Milki.Extensions.MixPlayer.NAudioExtensions;
using Milki.Extensions.MixPlayer.NAudioExtensions.Wave;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using OsuRTDataProvider.Mods;

namespace KeyAsio.Gui;

public class MusicTrack
{
    private static readonly ILogger Logger = SharedUtils.GetLogger(nameof(MusicTrack));

    private SeekableCachedSoundSampleProvider? _bgmCachedSoundSampleProvider;
    private VariableSpeedSampleProvider? _variableSampleProvider;
    private VolumeSampleProvider? _volumeSampleProvider;
    private ISampleProvider? _actualSampleProvider;

    public MusicTrack()
    {
    }

    public int LeadInMilliseconds { get; set; }
    public int Offset { get; set; }
    public ModsInfo.Mods PlayMods { get; set; }
    private AudioPlaybackEngine? AudioPlaybackEngine => SharedViewModel.Instance.AudioPlaybackEngine;

    public void PlaySingleAudio(CachedSound? cachedSound, float volume, int playTime)
    {
        if (cachedSound is null)
        {
            Logger.DebuggingWarn("Fail to play: CachedSound not found");
            return;
        }

        GetPlayInfoByPlayMod(ref playTime, PlayMods, out var keepTune, out var keepSpeed, out var playbackRate,
            out var diffTolerance);

        var timeSpan = TimeSpan.FromMilliseconds(playTime);
        if (_bgmCachedSoundSampleProvider == null)
        {
            if (playbackRate == 1)
            {
                //Logger.DebuggingError("WHAT");
            }

            _bgmCachedSoundSampleProvider = new SeekableCachedSoundSampleProvider(cachedSound, 2000 + LeadInMilliseconds) { PlayTime = timeSpan };

            ISampleProvider sampleProvider = _bgmCachedSoundSampleProvider;
            sampleProvider = _volumeSampleProvider = new VolumeSampleProvider(sampleProvider) { Volume = volume };
            if (!keepSpeed)
            {
                sampleProvider = _variableSampleProvider =
                    new VariableSpeedSampleProvider(sampleProvider, 10, new VariableSpeedOptions(keepTune, false))
                    {
                        PlaybackRate = playbackRate
                    };
            }

            _actualSampleProvider = sampleProvider;
            AudioPlaybackEngine?.AddMixerInput(sampleProvider);
        }
        else
        {
            if (_volumeSampleProvider != null)
            {
                _volumeSampleProvider.Volume = volume;
            }

            var currentPlayTime = _bgmCachedSoundSampleProvider.PlayTime;
            var diff = Math.Abs((currentPlayTime - timeSpan).TotalMilliseconds);
            if (diff > diffTolerance)
            {
                if (playbackRate == 1)
                {
                    //Logger.DebuggingError("WHAT");
                }

                Logger.DebuggingWarn($"Music offset too large ({diff:N2}ms), will force to seek.");
                _bgmCachedSoundSampleProvider.PlayTime = timeSpan;
                if (_variableSampleProvider != null)
                {
                    _variableSampleProvider.PlaybackRate = playbackRate;
                    _variableSampleProvider.SetSoundTouchProfile(new VariableSpeedOptions(keepTune, false));
                }
            }
        }
    }

    public void StopMusic()
    {
        AudioPlaybackEngine?.RootMixer.RemoveMixerInput(_actualSampleProvider);
        _variableSampleProvider?.Dispose();
        _bgmCachedSoundSampleProvider = null;
        //_bgmCachedSound = null;
    }

    private static void GetPlayInfoByPlayMod(ref int playTime, ModsInfo.Mods playMods, out bool keepTune, out bool keepSpeed,
        out float playbackRate, out int diffTolerance)
    {
        diffTolerance = 8;
        keepTune = false;
        keepSpeed = true;
        playbackRate = 1f;
        playTime += 8;
        if ((playMods & ModsInfo.Mods.Nightcore) != 0)
        {
            playTime += 90;
            diffTolerance = 55;
            keepSpeed = false;
            keepTune = false;
            playbackRate = 1.5f;
            //Logger.DebuggingWarn("Nightcore Mode");
        }
        else if ((playMods & ModsInfo.Mods.DoubleTime) != 0)
        {
            playTime += 95;
            diffTolerance = 55;
            keepSpeed = false;
            keepTune = true;
            playbackRate = 1.5f;
            //Logger.DebuggingWarn("DoubleTime Mode");
        }
        else if ((playMods & ModsInfo.Mods.HalfTime) != 0)
        {
            playTime += 75;
            diffTolerance = 50;
            keepSpeed = false;
            keepTune = true;
            playbackRate = 0.75f;
            //Logger.DebuggingWarn("HalfTime Mode");
        }
    }
}