using KeyAsio.Audio;
using KeyAsio.Audio.Caching;
using KeyAsio.Audio.SampleProviders;
using KeyAsio.MemoryReading;
using KeyAsio.MemoryReading.Logging;
using KeyAsio.Shared.Models;
using Milki.Extensions.Configuration;
using NAudio.Wave;

namespace KeyAsio.Shared.Realtime.Tracks;

public class SingleSynchronousTrack
{
    private static readonly ILogger Logger = LogUtils.GetLogger(nameof(SingleSynchronousTrack));

    private readonly VariableSpeedOptions _sharedVariableSpeedOptions = new(true, false);

    private SeekableCachedAudioSampleProvider? _bgmCachedSoundSampleProvider;
    private VariableSpeedSampleProvider? _variableSampleProvider;
    private ISampleProvider? _baseSampleProvider;

    private CachedAudio? _cachedSound;

    private readonly SharedViewModel _sharedViewModel;

    public SingleSynchronousTrack(SharedViewModel sharedViewModel)
    {
        _sharedViewModel = sharedViewModel;
    }

    public int LeadInMilliseconds { get; set; }
    public int Offset { get; set; }
    public Mods PlayMods { get; set; }
    private AudioEngine? AudioEngine => _sharedViewModel.AudioEngine;

    public void SyncAudio(CachedAudio? cachedSound, int playTime)
    {
        if (!ConfigurationFactory.GetConfiguration<AppSettings>().RealtimeOptions.EnableMusicFunctions) return;
        if (_cachedSound?.SourceHash != cachedSound?.SourceHash)
        {
            ClearAudio();
            _cachedSound = cachedSound;
        }

        if (cachedSound is null)
        {
            Logger.Warn("Fail to sync: CachedSound is null");
            return;
        }

        if (_bgmCachedSoundSampleProvider == null)
        {
            SetNewMixerInput(cachedSound, playTime);
        }
        else
        {
            UpdateCurrentMixerInput(_bgmCachedSoundSampleProvider, playTime);
        }
    }

    public void ClearAudio()
    {
        AudioEngine?.MusicMixer.RemoveMixerInput(_baseSampleProvider);
        if (_variableSampleProvider != null)
        {
            _variableSampleProvider.Dispose();
            _variableSampleProvider = null;
        }

        _bgmCachedSoundSampleProvider = null;
    }

    private void SetNewMixerInput(CachedAudio cachedSound, int playTime)
    {
        GetPlayInfoByPlayMod(ref playTime, PlayMods, out var keepTune, out var keepSpeed, out var playbackRate,
            out var diffTolerance);
        var timeSpan = TimeSpan.FromMilliseconds(playTime);

        _bgmCachedSoundSampleProvider = new SeekableCachedAudioSampleProvider(cachedSound,
            (int.MaxValue / 100) + LeadInMilliseconds)
        {
            PlayTime = timeSpan
        };
        var builder = new SampleProviderBuilder(_bgmCachedSoundSampleProvider);

        if (!keepSpeed)
        {
            _sharedVariableSpeedOptions.KeepTune = keepTune;
            _variableSampleProvider = builder.AddSampleProvider(k =>
                new VariableSpeedSampleProvider(k, 10, _sharedVariableSpeedOptions)
                {
                    PlaybackRate = playbackRate
                });
        }

        _baseSampleProvider = builder.CurrentSampleProvider;
        AudioEngine?.MusicMixer.AddMixerInput(_baseSampleProvider);
    }

    private void UpdateCurrentMixerInput(SeekableCachedAudioSampleProvider sampleProvider, int playTime)
    {
        GetPlayInfoByPlayMod(ref playTime, PlayMods, out var keepTune, out var keepSpeed, out var playbackRate,
            out var diffTolerance);
        var timeSpan = TimeSpan.FromMilliseconds(playTime);

        var currentPlayTime = sampleProvider.PlayTime;
        var diffMilliseconds = Math.Abs((currentPlayTime - timeSpan).TotalMilliseconds);
        if (diffMilliseconds > diffTolerance)
        {
            Logger.Debug($"Music offset too large {diffMilliseconds:N2}ms for {diffTolerance:N0}ms, will force to seek.");
            sampleProvider.PlayTime = timeSpan;
        }

        if (_variableSampleProvider != null)
        {
            _sharedVariableSpeedOptions.KeepTune = keepTune;
            _variableSampleProvider.PlaybackRate = playbackRate;
            _variableSampleProvider.SetSoundTouchProfile(_sharedVariableSpeedOptions);
        }
    }

    private static void GetPlayInfoByPlayMod(ref int playTime, Mods playMods, out bool keepTune, out bool keepSpeed,
        out float playbackRate, out int diffTolerance)
    {
        diffTolerance = 10;
        keepTune = false;
        keepSpeed = true;
        playbackRate = 1f;
        if (playMods != Mods.Unknown && (playMods & Mods.Nightcore) != 0)
        {
            playTime += 100;
            diffTolerance = 55;
            keepSpeed = false;
            keepTune = false;
            playbackRate = 1.5f;
        }
        else if (playMods != Mods.Unknown && (playMods & Mods.DoubleTime) != 0)
        {
            playTime += 100;
            diffTolerance = 55;
            keepSpeed = false;
            keepTune = true;
            playbackRate = 1.5f;
        }
        else if (playMods != Mods.Unknown && (playMods & Mods.HalfTime) != 0)
        {
            playTime += 80;
            diffTolerance = 50;
            keepSpeed = false;
            keepTune = true;
            playbackRate = 0.75f;
        }
        else
        {
            playTime += 30;
        }
    }
}