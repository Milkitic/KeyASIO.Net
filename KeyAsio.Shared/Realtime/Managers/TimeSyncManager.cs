using System.Diagnostics;
using Coosu.Beatmap;
using KeyAsio.MemoryReading;
using KeyAsio.MemoryReading.Logging;
using KeyAsio.Shared.Models;
using KeyAsio.Shared.Realtime.Tracks;
using Milki.Extensions.Configuration;
using Milki.Extensions.MixPlayer.NAudioExtensions.Wave;

namespace KeyAsio.Shared.Realtime.Managers;

/// <summary>
/// 时间同步管理器，负责处理播放时间的同步逻辑
/// </summary>
public class TimeSyncManager : ViewModelBase
{
    private static readonly ILogger Logger = LogUtils.GetLogger(nameof(TimeSyncManager));

    private int _playTime;
    private int _lastFetchedPlayTime;
    private bool _firstStartInitialized;
    private readonly Stopwatch _playTimeStopwatch = new();
    private readonly SingleSynchronousTrack _singleSynchronousTrack;

    public TimeSyncManager(SingleSynchronousTrack singleSynchronousTrack)
    {
        _singleSynchronousTrack = singleSynchronousTrack;
    }

    public int PlayTime
    {
        get => _playTime;
        set
        {
            value += AppSettings.RealtimeOptions.RealtimeModeAudioOffset + (int)_playTimeStopwatch.ElapsedMilliseconds;
            var val = _playTime;
            if (SetField(ref _playTime, value))
            {
                OnFetchedPlayTimeChanged(val, value);
            }
            else
            {
                OnFetchedPlayTimeChanged(val, value, true);
            }
        }
    }

    public int LastFetchedPlayTime
    {
        get => _lastFetchedPlayTime;
        set
        {
            if (SetField(ref _lastFetchedPlayTime, value))
            {
                _playTimeStopwatch.Restart();
            }
            else
            {
                _playTimeStopwatch.Reset();
            }

            PlayTime = value;
        }
    }

    public bool FirstStartInitialized
    {
        get => _firstStartInitialized;
        set => SetField(ref _firstStartInitialized, value);
    }

    public AppSettings AppSettings => ConfigurationFactory.GetConfiguration<AppSettings>();

    public event EventHandler<PlayTimeChangedEventArgs>? PlayTimeChanged;
    public event EventHandler? RetryDetected;

    private void OnFetchedPlayTimeChanged(int oldMs, int newMs, bool paused = false)
    {
        PlayTimeChanged?.Invoke(this, new PlayTimeChangedEventArgs(oldMs, newMs, paused));
    }

    public void HandlePlayTimeChange(int oldMs, int newMs, bool paused, bool isStarted,
        OsuFile? osuFile, string? audioFilename, string? folder, Mods playMods, bool result)
    {
        if (isStarted && oldMs > newMs) // Retry
        {
            RetryDetected?.Invoke(this, EventArgs.Empty);
            _firstStartInitialized = true;
            return;
        }

        var enableMusicFunctions = AppSettings.RealtimeOptions.EnableMusicFunctions;
        if (enableMusicFunctions && isStarted)
        {
            if (_firstStartInitialized && osuFile != null && audioFilename != null && folder != null && SharedViewModel.Instance.AudioEngine != null)
            {
                const int playingPauseThreshold = 5;
                var pauseCount = PlayTimeChanged?.GetInvocationList().Length ?? 0; // This is a simplified approach

                if (pauseCount >= playingPauseThreshold)
                {
                    _singleSynchronousTrack.ClearAudio();
                }
                else
                {
                    var musicPath = Path.Combine(folder, audioFilename);
                    if (CachedSoundFactory.ContainsCache(musicPath))
                    {
                        //todo: online offset && local offset
                        const int codeLatency = -1;
                        const int osuForceLatency = 15;
                        var oldMapForceOffset = osuFile.Version < 5 ? 24 : 0;
                        _singleSynchronousTrack.Offset = osuForceLatency + codeLatency + oldMapForceOffset;
                        _singleSynchronousTrack.LeadInMilliseconds = osuFile.General.AudioLeadIn;
                        if (!result)
                        {
                            _singleSynchronousTrack.PlayMods = playMods;
                        }

                        _singleSynchronousTrack.SyncAudio(CachedSoundFactory.GetCacheSound(musicPath), newMs);
                    }
                }
            }
        }
    }

    public void Reset()
    {
        _playTime = 0;
        _firstStartInitialized = false;
        _playTimeStopwatch.Reset();
    }
}

public class PlayTimeChangedEventArgs : EventArgs
{
    public int OldTime { get; }
    public int NewTime { get; }
    public bool IsPaused { get; }

    public PlayTimeChangedEventArgs(int oldTime, int newTime, bool isPaused)
    {
        OldTime = oldTime;
        NewTime = newTime;
        IsPaused = isPaused;
    }
}