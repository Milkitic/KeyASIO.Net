using System.Diagnostics;
using System.Text;
using Coosu.Beatmap;
using Coosu.Beatmap.Extensions.Playback;
using Coosu.Beatmap.Sections.GamePlay;
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
using Milki.Extensions.Configuration;
using OsuMemoryDataProvider;

namespace KeyAsio.Shared.Realtime;

public class RealtimeModeManager : ViewModelBase
{
    private int _playTime;

    private readonly Lock _isStartedLock = new();

    private string? _username;
    private Mods _playMods;
    private int _lastFetchedPlayTime;
    private int _combo;
    private int _score;
    private OsuMemoryStatus _osuStatus;
    private BeatmapIdentifier _beatmap;
    private bool _isStarted;

    private readonly ILogger<RealtimeModeManager> _logger;
    private readonly AudioEngine _audioEngine;
    private readonly SharedViewModel _sharedViewModel;
    private readonly StandardAudioProvider _standardAudioProvider;
    private readonly ManiaAudioProvider _maniaAudioProvider;
    private readonly Stopwatch _playTimeStopwatch = new();

    private readonly Dictionary<GameMode, IAudioProvider> _audioProviderDictionary;

    private readonly RealtimeStateMachine _stateMachine;
    private readonly AudioCacheService _audioCacheService;
    private readonly HitsoundNodeService _hitsoundNodeService;
    private readonly MusicTrackService _musicTrackService;
    private readonly AudioPlaybackService _audioPlaybackService;
    private readonly AudioCacheManager _audioCacheManager;

    private bool _firstStartInitialized; // After starting a map and playtime to zero

    public RealtimeModeManager(ILogger<RealtimeModeManager> logger, IServiceProvider serviceProvider,
        AudioEngine audioEngine, SharedViewModel sharedViewModel, AudioCacheService audioCacheService,
        HitsoundNodeService hitsoundNodeService, MusicTrackService musicTrackService,
        AudioPlaybackService audioPlaybackService, AudioCacheManager audioCacheManager)
    {
        _logger = logger;
        _audioEngine = audioEngine;
        _sharedViewModel = sharedViewModel;
        _standardAudioProvider = new StandardAudioProvider(
            serviceProvider.GetRequiredService<ILogger<StandardAudioProvider>>(), _audioEngine, audioCacheService,
            this);
        _maniaAudioProvider = new ManiaAudioProvider(serviceProvider.GetRequiredService<ILogger<ManiaAudioProvider>>(),
            _audioEngine, audioCacheService, this);
        _audioProviderDictionary = new Dictionary<GameMode, IAudioProvider>()
        {
            [GameMode.Circle] = _standardAudioProvider,
            [GameMode.Taiko] = _standardAudioProvider,
            [GameMode.Catch] = _standardAudioProvider,
            [GameMode.Mania] = _maniaAudioProvider,
        };

        // Initialize realtime state machine with scene mappings
        _stateMachine = new RealtimeStateMachine(new Dictionary<OsuMemoryStatus, IRealtimeState>
        {
            [OsuMemoryStatus.Playing] =
                new PlayingState(audioEngine, audioCacheManager, musicTrackService, hitsoundNodeService,
                    audioPlaybackService, sharedViewModel, audioCacheService),
            [OsuMemoryStatus.ResultsScreen] = new ResultsState(musicTrackService),
            [OsuMemoryStatus.NotRunning] = new NotRunningState(musicTrackService),
            [OsuMemoryStatus.SongSelect] = new BrowsingState(musicTrackService),
            [OsuMemoryStatus.SongSelectEdit] = new BrowsingState(musicTrackService),
            [OsuMemoryStatus.MainMenu] = new BrowsingState(musicTrackService),
            [OsuMemoryStatus.MultiplayerSongSelect] = new BrowsingState(musicTrackService),
        });
        Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.AboveNormal;
        _audioCacheService = audioCacheService;
        _hitsoundNodeService = hitsoundNodeService;
        _musicTrackService = musicTrackService;
        _audioPlaybackService = audioPlaybackService;
        _audioCacheManager = audioCacheManager;
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
                AppSettings.PlayerBase64 = EncodeUtils.GetBase64String(value, Encoding.ASCII);
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

    public OsuFile? OsuFile { get; internal set; }

    public string? AudioFilename { get; set; }

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

    public AppSettings AppSettings => ConfigurationFactory.GetConfiguration<AppSettings>();
    public IReadOnlyList<HitsoundNode> PlaybackList => _hitsoundNodeService.PlaybackList;
    public List<PlayableNode> KeyList => _hitsoundNodeService.KeyList;

    public IEnumerable<PlaybackInfo> GetKeyAudio(int keyIndex, int keyTotal)
    {
        return GetCurrentAudioProvider().GetKeyAudio(keyIndex, keyTotal);
    }

    public IEnumerable<PlaybackInfo> GetPlaybackAudio(bool isAuto)
    {
        return GetCurrentAudioProvider().GetPlaybackAudio(isAuto);
    }

    public async Task StartAsync(string beatmapFilenameFull, string beatmapFilename)
    {
        try
        {
            Logger.Info("Start playing.");
            IsStarted = true;
            OsuFile = null;

            var folder = Path.GetDirectoryName(beatmapFilenameFull);
            var previousFolder = _musicTrackService.GetMainTrackFolder();
            if (previousFolder != null && previousFolder != folder)
            {
                Logger.Info("Cleaning caches caused by folder changing.");
                CleanAudioCaches();
            }

            if (folder == null)
            {
                throw new Exception("The beatmap folder is null!");
            }

            var osuFile = await _hitsoundNodeService.InitializeNodeListsAsync(folder, beatmapFilename,
                GetCurrentAudioProvider(), PlayMods);
            OsuFile = osuFile;
            AudioFilename = osuFile?.General?.AudioFilename;
            _musicTrackService.UpdateMainTrackContext(folder, AudioFilename);
            AddSkinCacheInBackground();
            ResetNodes();
        }
        catch (Exception ex)
        {
            IsStarted = false;
            _logger.LogError(ex, "Error while starting a beatmap. Filename: {BeatmapFilename}. FilenameReal: {OsuFile}",
                beatmapFilename, OsuFile);
            LogUtils.LogToSentry(MemoryReading.Logging.LogLevel.Error, "Error while starting a beatmap", ex, k =>
            {
                k.SetTag("osu.filename", beatmapFilename);
                k.SetTag("osu.filename_real", OsuFile?.ToString() ?? "");
            });
        }
    }

    public void Stop()
    {
        Logger.Info("Stop playing.");
        IsStarted = false;
        _firstStartInitialized = false;
        var mixer = _audioEngine.EffectMixer;
        _audioPlaybackService.ClearAllLoops(mixer);
        _musicTrackService.ClearMainTrackAudio();
        mixer?.RemoveAllMixerInputs();
        _playTime = 0;
        Combo = 0;

        if (OsuFile != null)
        {
            _musicTrackService.PlaySingleAudioPreview(OsuFile, _musicTrackService.GetPreviewAudioFilePath(),
                OsuFile.General.PreviewTime);
        }
    }

    private void CleanAudioCaches()
    {
        _audioCacheService.ClearCaches();
    }

    private void ResetNodes()
    {
        _hitsoundNodeService.ResetNodes(GetCurrentAudioProvider(), PlayTime);
    }

    private void AddSkinCacheInBackground()
    {
        var mainFolder = _musicTrackService.GetMainTrackFolder();
        var mainAudioFilename = _musicTrackService.GetMainAudioFilename();
        if (mainFolder == null)
        {
            Logger.Warn("Main track folder is null, stop adding cache.");
            return;
        }

        if (_audioEngine.CurrentDevice == null)
        {
            Logger.Warn($"AudioEngine is null, stop adding cache.");
            return;
        }

        _audioCacheService.SetContext(mainFolder, mainAudioFilename);
        _audioCacheService.PrecacheMusicAndSkinInBackground();
    }

    private IAudioProvider GetCurrentAudioProvider()
    {
        if (OsuFile == null) return _standardAudioProvider;
        return _audioProviderDictionary[OsuFile.General.Mode];
    }

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

    internal bool GetEnableMusicFunctions() => AppSettings.RealtimeOptions.EnableMusicFunctions;

    internal bool GetFirstStartInitialized() => _firstStartInitialized;
    internal void SetFirstStartInitialized(bool value) => _firstStartInitialized = value;

    internal void ClearMixerLoopsAndMainTrackAudio()
    {
        var mixer = _audioEngine.EffectMixer;
        _audioPlaybackService.ClearAllLoops(mixer);
        _musicTrackService.ClearMainTrackAudio();
        mixer?.RemoveAllMixerInputs();
    }

    internal void ResetNodesExternal() => _hitsoundNodeService.ResetNodes(GetCurrentAudioProvider(), PlayTime);
}