using System.Diagnostics;
using System.Text;
using Coosu.Beatmap;
using Coosu.Beatmap.Extensions.Playback;
using Coosu.Beatmap.Sections.GamePlay;
using KeyAsio.Audio;
using KeyAsio.Audio.Caching;
using KeyAsio.MemoryReading;
using KeyAsio.MemoryReading.Logging;
using KeyAsio.Shared.Models;
using KeyAsio.Shared.Realtime.AudioProviders;
using KeyAsio.Shared.Realtime.Services;
using KeyAsio.Shared.Realtime.States;
using KeyAsio.Shared.Utils;
using Milki.Extensions.Configuration;
using OsuMemoryDataProvider;

namespace KeyAsio.Shared.Realtime;

public class RealtimeModeManager : ViewModelBase
{
    private static readonly ILogger Logger = LogUtils.GetLogger(nameof(RealtimeModeManager));

    private int _playTime;
    private bool _previousSelectSongStatus = true;
    private int _pauseCount = 0;

    private readonly Lock _isStartedLock = new();

    private string? _username;
    private Mods _playMods;
    private int _lastFetchedPlayTime;
    private int _combo;
    private int _score;
    private OsuMemoryStatus _osuStatus;
    private BeatmapIdentifier _beatmap;
    private bool _isStarted;

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
    private readonly CachedAudioFactory _cachedAudioFactory;

    private string? _folder;
    private string? _audioFilePath;

    private bool _firstStartInitialized; // After starting a map and playtime to zero
    private bool _result;

    public RealtimeModeManager(AudioEngine audioEngine,
        SharedViewModel sharedViewModel,
        AudioCacheService audioCacheService,
        HitsoundNodeService hitsoundNodeService,
        MusicTrackService musicTrackService,
        AudioPlaybackService audioPlaybackService,
        CachedAudioFactory cachedAudioFactory)
    {
        _audioEngine = audioEngine;
        _sharedViewModel = sharedViewModel;
        _standardAudioProvider = new StandardAudioProvider(_audioEngine, this);
        _maniaAudioProvider = new ManiaAudioProvider(_audioEngine, this);
        // Track services initialized via field initializer

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
            [OsuMemoryStatus.Playing] = new PlayingState(audioEngine, cachedAudioFactory),
            [OsuMemoryStatus.ResultsScreen] = new ResultsState(),
            [OsuMemoryStatus.NotRunning] = new NotRunningState(),
            [OsuMemoryStatus.SongSelect] = new BrowsingState(),
            [OsuMemoryStatus.SongSelectEdit] = new BrowsingState(),
            [OsuMemoryStatus.MainMenu] = new BrowsingState(),
            [OsuMemoryStatus.MultiplayerSongSelect] = new BrowsingState(),
        });
        Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.AboveNormal;
        _audioCacheService = audioCacheService;
        _hitsoundNodeService = hitsoundNodeService;
        _musicTrackService = musicTrackService;
        _audioPlaybackService = audioPlaybackService;
        _cachedAudioFactory = cachedAudioFactory;
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

    public bool TryGetAudioByNode(HitsoundNode playableNode, out CachedAudio cachedSound)
    {
        if (!_audioCacheService.TryGetAudioByNode(playableNode, out cachedSound)) return false;
        return playableNode is not PlayableNode || cachedSound != null;
    }

    public IEnumerable<PlaybackInfo> GetKeyAudio(int keyIndex, int keyTotal)
    {
        return GetCurrentAudioProvider().GetKeyAudio(keyIndex, keyTotal);
    }

    public IEnumerable<PlaybackInfo> GetPlaybackAudio(bool isAuto)
    {
        return GetCurrentAudioProvider().GetPlaybackAudio(isAuto);
    }

    public void PlayAudio(PlaybackInfo playbackObject)
    {
        if (playbackObject.HitsoundNode is PlayableNode playableNode)
        {
            if (playbackObject.CachedSound != null)
            {
                var volume = playableNode.PlayablePriority == PlayablePriority.Effects
                    ? playableNode.Volume * 1.25f
                    : playableNode.Volume;
                _audioPlaybackService.PlayEffectsAudio(playbackObject.CachedSound, volume, playableNode.Balance,
                    AppSettings);
            }
        }
        else
        {
            var controlNode = (ControlNode)playbackObject.HitsoundNode;
            _audioPlaybackService.PlayLoopAudio(playbackObject.CachedSound, controlNode, AppSettings);
        }
    }

    public void PlayAudio(CachedAudio? cachedSound, float volume, float balance)
    {
        _audioPlaybackService.PlayEffectsAudio(cachedSound, volume, balance, AppSettings);
    }

    public async Task StartAsync(string beatmapFilenameFull, string beatmapFilename)
    {
        try
        {
            Logger.Info("Start playing.");
            IsStarted = true;
            OsuFile = null;

            var folder = Path.GetDirectoryName(beatmapFilenameFull);
            if (_folder != folder)
            {
                Logger.Info("Cleaning caches caused by folder changing.");
                CleanAudioCaches();
            }

            _folder = folder;
            if (folder == null)
            {
                throw new Exception("The beatmap folder is null!");
            }

            var osuFile = await _hitsoundNodeService.InitializeNodeListsAsync(folder, beatmapFilename, GetCurrentAudioProvider(), PlayMods);
            OsuFile = osuFile;
            AudioFilename = osuFile?.General?.AudioFilename;
            AddSkinCacheInBackground();
            ResetNodes();
        }
        catch (Exception ex)
        {
            IsStarted = false;
            Logger.Error(ex, $"Error while starting a beatmap. Filename: {beatmapFilename}. FilenameReal: {OsuFile}");
            LogUtils.LogToSentry(LogLevel.Error, "Error while starting a beatmap", ex, k =>
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

        if (_folder != null && OsuFile != null)
        {
            var path = Path.Combine(_folder, OsuFile.General.AudioFilename ?? "");
            _musicTrackService.PlaySingleAudioPreview(OsuFile, path, OsuFile.General.PreviewTime);
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
        if (_folder == null)
        {
            Logger.Warn($"{nameof(_folder)} is null, stop adding cache.");
            return;
        }

        if (_audioEngine.CurrentDevice == null)
        {
            Logger.Warn($"AudioEngine is null, stop adding cache.");
            return;
        }

        _audioCacheService.SetContext(_folder, AudioFilename);
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

    internal void StartLowPass(int lower, int upper)
    {
        _musicTrackService.StartLowPass(lower, upper);
    }

    internal bool TryGetCachedSound(string filenameWithoutExt, out CachedAudio? cachedSound)
    {
        return _audioCacheService.TryGetCachedSound(filenameWithoutExt, out cachedSound);
    }

    internal void StopCurrentMusic(int fadeMs = 0)
    {
        _musicTrackService.StopCurrentMusic(fadeMs);
    }

    internal void SetResultFlag(bool value)
    {
        _result = value;
    }

    internal void SetSingleTrackPlayMods(Mods mods)
    {
        _musicTrackService.SetSingleTrackPlayMods(mods);
    }

    internal string? GetAudioFilePath() => _audioFilePath;

    internal void UpdateAudioPreviewContext(string folder, string? audioFilePath)
    {
        _folder = folder;
        _audioFilePath = audioFilePath;
    }

    internal void ResetBrowsingPauseState()
    {
        _previousSelectSongStatus = true;
        _pauseCount = 0;
    }

    internal void PlaySingleAudioPreview(OsuFile osuFile, string? path, int playTime)
    {
        if (path is null) return;
        _musicTrackService.PlaySingleAudioPreview(osuFile, path, playTime);
    }

    internal void UpdatePauseCount(bool paused)
    {
        if (paused && _previousSelectSongStatus)
        {
            _pauseCount++;
        }
        else if (!paused)
        {
            _pauseCount = 0;
        }
    }

    internal bool GetPreviousSelectSongStatus() => _previousSelectSongStatus;
    internal void SetPreviousSelectSongStatus(bool value) => _previousSelectSongStatus = value;
    internal int GetPauseCount() => _pauseCount;
    internal void SetPauseCount(int value) => _pauseCount = value;
    internal bool GetEnableMusicFunctions() => AppSettings.RealtimeOptions.EnableMusicFunctions;

    internal void PauseCurrentMusic()
    {
        _musicTrackService.PauseCurrentMusic();
    }

    internal void RecoverCurrentMusic()
    {
        _musicTrackService.RecoverCurrentMusic();
    }

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

    internal string? GetMusicPath()
    {
        if (_folder == null || AudioFilename == null) return null;
        return Path.Combine(_folder, AudioFilename);
    }

    internal void SetMainTrackOffsetAndLeadIn(int offset, int leadInMs)
    {
        _musicTrackService.SetMainTrackOffsetAndLeadIn(offset, leadInMs);
    }

    internal bool IsResultFlag() => _result;

    internal void SyncMainTrackAudio(CachedAudio sound, int positionMs)
    {
        _musicTrackService.SyncMainTrackAudio(sound, positionMs);
    }

    internal void ClearMainTrackAudio()
    {
        _musicTrackService.ClearMainTrackAudio();
    }

    internal void AdvanceCachingWindow(int newMs)
    {
        _hitsoundNodeService.AdvanceCachingWindow(newMs);
    }

    internal void PlayAutoPlaybackIfNeeded()
    {
        if (_sharedViewModel.AutoMode || (PlayMods & Mods.Autoplay) != 0 || IsReplay)
        {
            foreach (var playbackObject in GetPlaybackAudio(false))
            {
                PlayAudio(playbackObject);
            }
        }
    }

    internal void PlayManualPlaybackIfNeeded()
    {
        foreach (var playbackObject in GetPlaybackAudio(true))
        {
            PlayAudio(playbackObject);
        }
    }
}