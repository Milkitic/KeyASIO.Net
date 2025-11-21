using System.Diagnostics;
using System.Text;
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

public class RealtimeProperties : ViewModelBase
{
    public Func<int, int, ValueTask>? OnComboChanged;
    public Func<Mods, Mods, ValueTask>? OnPlayModsChanged;
    public Func<int, int, bool, ValueTask>? OnFetchedPlayTimeChanged;
    public Func<OsuMemoryStatus, OsuMemoryStatus, ValueTask>? OnStatusChanged;
    public Func<BeatmapIdentifier, BeatmapIdentifier, ValueTask>? OnBeatmapChanged;

    private readonly AppSettings _appSettings;
    private readonly Stopwatch _playTimeStopwatch = new();

    private int _playTime;
    private string? _username;
    private Mods _playMods;
    private int _lastFetchedPlayTime;
    private int _combo;
    private int _score;
    private OsuMemoryStatus _osuStatus;
    private BeatmapIdentifier _beatmap;

    public RealtimeProperties(AppSettings appSettings)
    {
        _appSettings = appSettings;
    }

    public bool IsStarted { get; set; }
    public bool IsReplay { get; set; }

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
                OnPlayModsChanged?.Invoke(val, value);
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
                OnFetchedPlayTimeChanged?.Invoke(val, value, false);
            }
            else
            {
                OnFetchedPlayTimeChanged?.Invoke(val, value, true);
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
                OnComboChanged?.Invoke(val, value);
            }
        }
    }

    public int Score
    {
        get => _score;
        set => SetField(ref _score, value);
    }

    public OsuMemoryStatus OsuStatus
    {
        get => _osuStatus;
        set
        {
            var val = _osuStatus;
            if (SetField(ref _osuStatus, value))
            {
                OnStatusChanged?.Invoke(val, value);
            }
        }
    }

    public BeatmapIdentifier Beatmap
    {
        get => _beatmap;
        set
        {
            var val = _beatmap;
            if (SetField(ref _beatmap, value))
            {
                OnBeatmapChanged?.Invoke(val, value);
            }
        }
    }
}

public class RealtimeModeManager
{
    private readonly RealtimeProperties _realtimeProperties;
    private readonly RealtimeStateMachine _stateMachine;

    public RealtimeModeManager(IServiceProvider serviceProvider,
        AppSettings appSettings,
        AudioEngine audioEngine,
        SharedViewModel sharedViewModel,
        AudioCacheService audioCacheService,
        HitsoundNodeService hitsoundNodeService,
        MusicTrackService musicTrackService,
        AudioPlaybackService audioPlaybackService,
        AudioCacheManager audioCacheManager,
        PlaySessionManager playSessionManager,
        RealtimeProperties realtimeProperties)
    {
        _realtimeProperties = realtimeProperties;
        _realtimeProperties.OnBeatmapChanged = OnBeatmapChanged;
        _realtimeProperties.OnComboChanged = OnComboChanged;
        _realtimeProperties.OnStatusChanged = OnStatusChanged;
        _realtimeProperties.OnPlayModsChanged = OnPlayModsChanged;
        _realtimeProperties.OnFetchedPlayTimeChanged = OnFetchedPlayTimeChanged;

        var standardAudioProvider = new StandardAudioProvider(
            serviceProvider.GetRequiredService<ILogger<StandardAudioProvider>>(),
            appSettings, realtimeProperties, audioEngine, audioCacheService, playSessionManager);
        var maniaAudioProvider = new ManiaAudioProvider(
            serviceProvider.GetRequiredService<ILogger<ManiaAudioProvider>>(),
            appSettings, realtimeProperties, audioEngine, audioCacheService, playSessionManager);
        playSessionManager.InitializeProviders(standardAudioProvider, maniaAudioProvider);

        // Initialize realtime state machine with scene mappings
        _stateMachine = new RealtimeStateMachine(new Dictionary<OsuMemoryStatus, IRealtimeState>
        {
            [OsuMemoryStatus.Playing] = new PlayingState(appSettings, audioEngine, audioCacheManager, musicTrackService,
                hitsoundNodeService, audioPlaybackService, sharedViewModel, playSessionManager, audioCacheService),
            [OsuMemoryStatus.ResultsScreen] = new ResultsState(musicTrackService),
            [OsuMemoryStatus.NotRunning] = new NotRunningState(appSettings, musicTrackService),
            [OsuMemoryStatus.SongSelect] = new BrowsingState(appSettings, musicTrackService, playSessionManager),
            [OsuMemoryStatus.SongSelectEdit] = new BrowsingState(appSettings, musicTrackService, playSessionManager),
            [OsuMemoryStatus.MainMenu] = new BrowsingState(appSettings, musicTrackService, playSessionManager),
            [OsuMemoryStatus.MultiplayerSongSelect] =
                new BrowsingState(appSettings, musicTrackService, playSessionManager),
        });
        Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.AboveNormal;
    }


    private async ValueTask OnComboChanged(int oldCombo, int newCombo)
    {
        _stateMachine.Current?.OnComboChanged(_realtimeProperties, oldCombo, newCombo);
    }

    private async ValueTask OnStatusChanged(OsuMemoryStatus oldStatus, OsuMemoryStatus newStatus)
    {
        await _stateMachine.TransitionToAsync(_realtimeProperties, newStatus);
    }

    private async ValueTask OnBeatmapChanged(BeatmapIdentifier oldBeatmap, BeatmapIdentifier newBeatmap)
    {
        _stateMachine.Current?.OnBeatmapChanged(_realtimeProperties, newBeatmap);
    }

    private async ValueTask OnPlayModsChanged(Mods oldMods, Mods newMods)
    {
        _stateMachine.Current?.OnModsChanged(_realtimeProperties, oldMods, newMods);
    }

    private async ValueTask OnFetchedPlayTimeChanged(int oldMs, int newMs, bool paused = false)
    {
        _stateMachine.Current?.OnPlayTimeChanged(_realtimeProperties, oldMs, newMs, paused);
    }
}