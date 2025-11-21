using System.Diagnostics;
using System.Text;
using Coosu.Beatmap;
using KeyAsio.Audio;
using KeyAsio.Audio.Caching;
using KeyAsio.MemoryReading;
using KeyAsio.Shared.Models;
using KeyAsio.Shared.Realtime.AudioProviders;
using KeyAsio.Shared.Realtime.Services;
using KeyAsio.Shared.Realtime.States;
using KeyAsio.Shared.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OsuMemoryDataProvider;

namespace KeyAsio.Shared.Realtime;

public class RealtimeModeManager : ViewModelBase, IRealtimeContext
{
    private int _playTime;

    private string? _username;
    private Mods _playMods;
    private int _lastFetchedPlayTime;
    private int _combo;
    private int _score;
    private OsuMemoryStatus _osuStatus;
    private BeatmapIdentifier _beatmap;
    private bool _isStarted;

    private readonly AppSettings _appSettings;
    private readonly Stopwatch _playTimeStopwatch = new();
    private readonly PlaySessionService _playSessionService;
    private readonly RealtimeStateMachine _stateMachine;

    private readonly Lock _isStartedLock = new();

    public RealtimeModeManager(ILogger<RealtimeModeManager> logger,
        IServiceProvider serviceProvider,
        AppSettings appSettings,
        AudioEngine audioEngine,
        SharedViewModel sharedViewModel,
        AudioCacheService audioCacheService,
        HitsoundNodeService hitsoundNodeService,
        MusicTrackService musicTrackService,
        AudioPlaybackService audioPlaybackService,
        AudioCacheManager audioCacheManager,
        PlaySessionService playSessionService)
    {
        _appSettings = appSettings;
        _playSessionService = playSessionService;
        var standardAudioProvider = new StandardAudioProvider(
            serviceProvider.GetRequiredService<ILogger<StandardAudioProvider>>(),
            this, audioEngine, audioCacheService, _playSessionService);
        var maniaAudioProvider = new ManiaAudioProvider(
            serviceProvider.GetRequiredService<ILogger<ManiaAudioProvider>>(),
            this, audioEngine, audioCacheService, _playSessionService);
        _playSessionService.InitializeProviders(standardAudioProvider, maniaAudioProvider);

        // Initialize realtime state machine with scene mappings
        _stateMachine = new RealtimeStateMachine(new Dictionary<OsuMemoryStatus, IRealtimeState>
        {
            [OsuMemoryStatus.Playing] = new PlayingState(appSettings, audioEngine, audioCacheManager, musicTrackService,
                hitsoundNodeService, audioPlaybackService, sharedViewModel, audioCacheService),
            [OsuMemoryStatus.ResultsScreen] = new ResultsState(musicTrackService),
            [OsuMemoryStatus.NotRunning] = new NotRunningState(appSettings, musicTrackService),
            [OsuMemoryStatus.SongSelect] = new BrowsingState(appSettings, musicTrackService),
            [OsuMemoryStatus.SongSelectEdit] = new BrowsingState(appSettings, musicTrackService),
            [OsuMemoryStatus.MainMenu] = new BrowsingState(appSettings, musicTrackService),
            [OsuMemoryStatus.MultiplayerSongSelect] = new BrowsingState(appSettings, musicTrackService),
        });
        Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.AboveNormal;
    }

    public string? Username
    {
        get => _username;
        set
        {
            if (_username == value) return;
            _username = value;
            if (!string.IsNullOrEmpty(value))
            {
                _appSettings.PlayerBase64 = EncodeUtils.GetBase64String(value, Encoding.ASCII);
            }

            OnPropertyChanged();
        }
    }

    public Mods PlayMods
    {
        get => _playMods;
        set
        {
            var val = _playMods;
            if (SetField(ref _playMods, value))
            {
                OnPlayModsChanged(val, value);
            }
        }
    }

    public int PlayTime
    {
        get => _playTime;
        set
        {
            value += _appSettings.RealtimeOptions.RealtimeModeAudioOffset + (int)_playTimeStopwatch.ElapsedMilliseconds;
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

    public int Combo
    {
        get => _combo;
        set
        {
            var val = _combo;
            if (SetField(ref _combo, value))
            {
                OnComboChanged(val, value);
            }
        }
    }

    public int Score
    {
        get => _score;
        set => SetField(ref _score, value);
    }

    public bool IsReplay { get; set; }

    public OsuMemoryStatus OsuStatus
    {
        get => _osuStatus;
        set
        {
            if (SetField(ref _osuStatus, value))
            {
                _ = OnStatusChanged(_osuStatus);
            }
        }
    }

    public OsuFile? OsuFile => _playSessionService.OsuFile;

    public BeatmapIdentifier Beatmap
    {
        get => _beatmap;
        set
        {
            if (SetField(ref _beatmap, value))
            {
                OnBeatmapChanged(value);
            }
        }
    }

    public bool IsStarted
    {
        get
        {
            lock (_isStartedLock)
            {
                return _isStarted;
            }
        }
        set
        {
            lock (_isStartedLock)
            {
                SetField(ref _isStarted, value);
            }
        }
    }

    public void FillKeyAudio(List<PlaybackInfo> buffer, int keyIndex, int keyTotal)
    {
        CurrentAudioProvider.FillKeyAudio(buffer, keyIndex, keyTotal);
    }

    public void FillPlaybackAudio(List<PlaybackInfo> buffer, bool isAuto)
    {
        CurrentAudioProvider.FillPlaybackAudio(buffer, isAuto);
    }

    public async Task StartAsync(string beatmapFilenameFull, string beatmapFilename)
    {
        await _playSessionService.StartAsync(beatmapFilenameFull, beatmapFilename, PlayMods, PlayTime);
        IsStarted = _playSessionService.IsStarted;
    }

    public void Stop()
    {
        _playSessionService.Stop();
        _playTime = 0;
        Combo = 0;
        IsStarted = _playSessionService.IsStarted;
    }

    public IAudioProvider CurrentAudioProvider => _playSessionService.CurrentAudioProvider;

    private void OnComboChanged(int oldCombo, int newCombo)
    {
        _stateMachine.Current?.OnComboChanged(this, oldCombo, newCombo);
    }

    private async Task OnStatusChanged(OsuMemoryStatus cur)
    {
        await _stateMachine.TransitionToAsync(this, cur);
    }

    private void OnBeatmapChanged(BeatmapIdentifier beatmap)
    {
        _stateMachine.Current?.OnBeatmapChanged(this, beatmap);
    }

    private void OnPlayModsChanged(Mods oldMods, Mods newMods)
    {
        _stateMachine.Current?.OnModsChanged(this, oldMods, newMods);
    }

    private void OnFetchedPlayTimeChanged(int oldMs, int newMs, bool paused = false)
    {
        _stateMachine.Current?.OnPlayTimeChanged(this, oldMs, newMs, paused);
    }
}