using System;
using KeyAsio.Gui.Models;
using KeyAsio.Gui.Utils;
using KeyAsio.Gui.Waves;
using Microsoft.Extensions.Logging;
using Milki.Extensions.MixPlayer.NAudioExtensions;
using Milki.Extensions.MixPlayer.NAudioExtensions.Wave;
using NAudio.Wave.SampleProviders;
using OsuRTDataProvider.Mods;

namespace KeyAsio.Gui.Realtime.Tracks;

public class SingleSynchronousTrack
{
    private static readonly ILogger Logger = SharedUtils.GetLogger(nameof(SingleSynchronousTrack));

    private SeekableCachedSoundSampleProvider? _bgmCachedSoundSampleProvider;
    private VariableSpeedSampleProvider? _variableSampleProvider;
    private VolumeSampleProvider? _volumeSampleProvider;

    private readonly VariableSpeedOptions _sharedVariableSpeedOptions = new(true, false);
    public SingleSynchronousTrack()
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

        if (_bgmCachedSoundSampleProvider == null)
        {
            SetNewMixerInput(cachedSound, volume, playTime);
        }
        else
        {
            UpdateCurrentMixerInput(_bgmCachedSoundSampleProvider, volume, playTime);
        }
    }

    public void StopMusic()
    {
        AudioPlaybackEngine?.RootMixer.RemoveMixerInput(_volumeSampleProvider);
        _variableSampleProvider?.Dispose();
        _bgmCachedSoundSampleProvider = null;
    }

    private void SetNewMixerInput(CachedSound cachedSound, float volume, int playTime)
    {
        GetPlayInfoByPlayMod(ref playTime, PlayMods, out var keepTune, out var keepSpeed, out var playbackRate,
            out var diffTolerance);
        var timeSpan = TimeSpan.FromMilliseconds(playTime);

        _bgmCachedSoundSampleProvider = new SeekableCachedSoundSampleProvider(cachedSound, 2000 + LeadInMilliseconds)
        { PlayTime = timeSpan };

        if (!keepSpeed)
        {
            _sharedVariableSpeedOptions.KeepTune = keepTune;
            _variableSampleProvider =
                new VariableSpeedSampleProvider(_bgmCachedSoundSampleProvider, 10, _sharedVariableSpeedOptions)
                {
                    PlaybackRate = playbackRate
                };
            _volumeSampleProvider = new VolumeSampleProvider(_variableSampleProvider) { Volume = volume };
        }
        else
        {
            _volumeSampleProvider = new VolumeSampleProvider(_bgmCachedSoundSampleProvider) { Volume = volume };
        }

        AudioPlaybackEngine?.AddMixerInput(_volumeSampleProvider);
    }

    private void UpdateCurrentMixerInput(SeekableCachedSoundSampleProvider sampleProvider, float volume, int playTime)
    {
        GetPlayInfoByPlayMod(ref playTime, PlayMods, out var keepTune, out var keepSpeed, out var playbackRate,
            out var diffTolerance);
        var timeSpan = TimeSpan.FromMilliseconds(playTime);

        var currentPlayTime = sampleProvider.PlayTime;
        var diffMilliseconds = Math.Abs((currentPlayTime - timeSpan).TotalMilliseconds);
        if (diffMilliseconds > diffTolerance)
        {
            Logger.DebuggingDebug($"Music offset too large {diffMilliseconds:N2}ms for {diffTolerance:N0}ms, will force to seek.");
            sampleProvider.PlayTime = timeSpan;
        }

        if (_variableSampleProvider != null)
        {
            _variableSampleProvider.PlaybackRate = playbackRate;
            _sharedVariableSpeedOptions.KeepTune = keepTune;
            _variableSampleProvider.SetSoundTouchProfile(_sharedVariableSpeedOptions);
        }

        if (_volumeSampleProvider != null)
        {
            _volumeSampleProvider.Volume = volume;
        }
    }

    private static void GetPlayInfoByPlayMod(ref int playTime, ModsInfo.Mods playMods, out bool keepTune, out bool keepSpeed,
        out float playbackRate, out int diffTolerance)
    {
        diffTolerance = 8;
        keepTune = false;
        keepSpeed = true;
        playbackRate = 1f;
        if (playMods != ModsInfo.Mods.Unknown && (playMods & ModsInfo.Mods.Nightcore) != 0)
        {
            playTime += 100;
            diffTolerance = 55;
            keepSpeed = false;
            keepTune = false;
            playbackRate = 1.5f;
        }
        else if (playMods != ModsInfo.Mods.Unknown && (playMods & ModsInfo.Mods.DoubleTime) != 0)
        {
            playTime += 100;
            diffTolerance = 55;
            keepSpeed = false;
            keepTune = true;
            playbackRate = 1.5f;
        }
        else if (playMods != ModsInfo.Mods.Unknown && (playMods & ModsInfo.Mods.HalfTime) != 0)
        {
            playTime += 80;
            diffTolerance = 50;
            keepSpeed = false;
            keepTune = true;
            playbackRate = 0.75f;
        }
        else
        {
            playTime += 8;
        }
    }
}