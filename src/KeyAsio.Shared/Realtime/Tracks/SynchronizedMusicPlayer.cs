using KeyAsio.Audio;
using KeyAsio.Audio.Caching;
using KeyAsio.Audio.SampleProviders;
using KeyAsio.Shared.OsuMemory;
using Microsoft.Extensions.Logging;
using NAudio.Wave;

namespace KeyAsio.Shared.Realtime.Tracks;

public class SynchronizedMusicPlayer
{
    private readonly VariableSpeedOptions _sharedVariableSpeedOptions = new(true, false);

    private CachedAudioProvider? _bgmCachedAudioSampleProvider;
    private VariableSpeedSampleProvider? _variableSampleProvider;
    private ISampleProvider? _baseSampleProvider;

    private CachedAudio? _cachedAudio;

    private readonly ILogger<SynchronizedMusicPlayer> _logger;
    private readonly AppSettings _appSettings;
    private readonly AudioEngine _audioEngine;

    public SynchronizedMusicPlayer(ILogger<SynchronizedMusicPlayer> logger, AppSettings appSettings, AudioEngine audioEngine)
    {
        _logger = logger;
        _appSettings = appSettings;
        _audioEngine = audioEngine;
    }

    public int LeadInMilliseconds { get; set; }
    public int Offset { get; set; }
    public Mods PlayMods { get; set; }

    public void SyncAudio(CachedAudio? cachedAudio, int playTime)
    {
        if (!_appSettings.Realtime.RealtimeEnableMusic) return;
        if (_cachedAudio?.SourceHash != cachedAudio?.SourceHash)
        {
            ClearAudio();
            _cachedAudio = cachedAudio;
        }

        if (cachedAudio is null)
        {
            _logger.LogWarning("Fail to sync: CachedAudio is null");
            return;
        }

        if (_bgmCachedAudioSampleProvider == null)
        {
            SetNewMixerInput(cachedAudio, playTime);
        }
        else
        {
            UpdateCurrentMixerInput(_bgmCachedAudioSampleProvider, playTime);
        }
    }

    public void ClearAudio()
    {
        if (_baseSampleProvider != null) _audioEngine.MusicMixer.RemoveMixerInput(_baseSampleProvider);
        if (_variableSampleProvider != null)
        {
            _variableSampleProvider.Dispose();
            _variableSampleProvider = null;
        }

        _bgmCachedAudioSampleProvider = null;
    }

    private void SetNewMixerInput(CachedAudio cachedAudio, int playTime)
    {
        GetPlayInfoByPlayMod(ref playTime, PlayMods, out var keepTune, out var keepSpeed, out var playbackRate,
            out var diffTolerance);
        var timeSpan = TimeSpan.FromMilliseconds(playTime);

        _bgmCachedAudioSampleProvider = new CachedAudioProvider(cachedAudio)
        {
            ExcludeFromPool = true,
            PlayTime = timeSpan
        };
        var builder = new SampleProviderBuilder(_bgmCachedAudioSampleProvider);

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
        _audioEngine.MusicMixer.AddMixerInput(_baseSampleProvider);
    }

    private void UpdateCurrentMixerInput(CachedAudioProvider sampleProvider, int playTime)
    {
        GetPlayInfoByPlayMod(ref playTime, PlayMods, out var keepTune, out var keepSpeed, out var playbackRate,
            out var diffTolerance);
        var timeSpan = TimeSpan.FromMilliseconds(playTime);

        var currentPlayTime = sampleProvider.PlayTime;
        var diffMilliseconds = Math.Abs((currentPlayTime - timeSpan).TotalMilliseconds);
        if (diffMilliseconds > diffTolerance)
        {
            _logger.LogDebug("Music offset too large {Milliseconds:N2}ms for {Tolerance:N0}ms, will force to seek.",
                diffMilliseconds, diffTolerance);
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