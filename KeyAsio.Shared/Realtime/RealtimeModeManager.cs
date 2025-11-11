using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using Coosu.Beatmap;
using Coosu.Beatmap.Extensions.Playback;
using Coosu.Beatmap.Sections.GamePlay;
using KeyAsio.MemoryReading;
using KeyAsio.MemoryReading.Logging;
using KeyAsio.Shared.Audio;
using KeyAsio.Shared.Models;
using KeyAsio.Shared.Realtime.AudioProviders;
using KeyAsio.Shared.Realtime.Services;
using KeyAsio.Shared.Realtime.States;
using KeyAsio.Shared.Realtime.Tracks;
using KeyAsio.Shared.Utils;
using Milki.Extensions.Configuration;
using Milki.Extensions.MixPlayer.NAudioExtensions.Wave;
using OsuMemoryDataProvider;
using BalanceSampleProvider = KeyAsio.Shared.Audio.BalanceSampleProvider;

namespace KeyAsio.Shared.Realtime;

public class RealtimeModeManager : ViewModelBase
{
    private static readonly string[] SkinAudioFiles = ["combobreak"];

    public static RealtimeModeManager Instance { get; } = new();
    private static readonly ILogger Logger = LogUtils.GetLogger(nameof(RealtimeModeManager));

    private int _playTime;
    private bool _previousSelectSongStatus = true;
    private int _pauseCount = 0;

    private readonly Lock _isStartedLock = new();
    private readonly AudioCacheService _audioCacheService;

    private string? _username;
    private Mods _playMods;
    private int _lastFetchedPlayTime;
    private int _combo;
    private int _score;
    private OsuMemoryStatus _osuStatus;
    private BeatmapIdentifier _beatmap;
    private bool _isStarted;

    private readonly List<PlayableNode> _keyList = new();
    private readonly List<HitsoundNode> _playbackList = new();

    private readonly StandardAudioProvider _standardAudioProvider;
    private readonly ManiaAudioProvider _maniaAudioProvider;
    private readonly Stopwatch _playTimeStopwatch = new();

    private readonly Dictionary<GameMode, IAudioProvider> _audioProviderDictionary;

    private readonly SingleSynchronousTrack _singleSynchronousTrack;
    private readonly SelectSongTrack _selectSongTrack;

    private readonly LoopProviders _loopProviders = new();

    private readonly RealtimeStateMachine _stateMachine;

    private string? _folder;
    private string? _audioFilePath;

    private int _nextCachingTime;
    private bool _firstStartInitialized; // After starting a map and playtime to zero
    private bool _result;

    public RealtimeModeManager()
    {
        _standardAudioProvider = new StandardAudioProvider(this);
        _maniaAudioProvider = new ManiaAudioProvider(this);
        _singleSynchronousTrack = new SingleSynchronousTrack();
        _selectSongTrack = new SelectSongTrack();

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
            [OsuMemoryStatus.Playing] = new PlayingState(),
            [OsuMemoryStatus.ResultsScreen] = new ResultsState(),
            [OsuMemoryStatus.NotRunning] = new NotRunningState(),
            [OsuMemoryStatus.SongSelect] = new BrowsingState(),
            [OsuMemoryStatus.SongSelectEdit] = new BrowsingState(),
            [OsuMemoryStatus.MainMenu] = new BrowsingState(),
            [OsuMemoryStatus.MultiplayerSongSelect] = new BrowsingState(),
        });
        Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.AboveNormal;
        _audioCacheService = new AudioCacheService(() => IsStarted);
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
    public IReadOnlyList<HitsoundNode> PlaybackList => _playbackList;
    public List<PlayableNode> KeyList => _keyList;

    public bool TryGetAudioByNode(HitsoundNode playableNode, out CachedSound? cachedSound)
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

                PlayAudio(playbackObject.CachedSound, volume, playableNode.Balance);
            }
        }
        else
        {
            var controlNode = (ControlNode)playbackObject.HitsoundNode;
            PlayLoopAudio(playbackObject.CachedSound, controlNode);
        }
    }

    public void PlayAudio(CachedSound? cachedSound, float volume, float balance)
    {
        if (cachedSound is null)
        {
            Logger.Warn("Fail to play: CachedSound not found");
            return;
        }

        if (AppSettings.RealtimeOptions.IgnoreLineVolumes)
        {
            volume = 1;
        }

        balance *= AppSettings.RealtimeOptions.BalanceFactor;
        try
        {
            SharedViewModel.Instance.AudioEngine?.EffectMixer.AddMixerInput(
                new BalanceSampleProvider(
                        new EnhancedVolumeSampleProvider(new SeekableCachedSoundSampleProvider(cachedSound))
                        { Volume = volume }
                    )
                { Balance = balance }
            );
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error occurs while playing audio.", true);
        }

        Logger.Debug($"Play {Path.GetFileNameWithoutExtension(cachedSound.SourcePath)}; " +
                     $"Vol. {volume}; " +
                     $"Bal. {balance}");
    }

    private void PlayLoopAudio(CachedSound? cachedSound, ControlNode controlNode)
    {
        var rootMixer = SharedViewModel.Instance.AudioEngine?.EffectMixer;
        if (rootMixer == null)
        {
            Logger.Warn($"RootMixer is null, stop adding cache.");
            return;
        }

        var volume = AppSettings.RealtimeOptions.IgnoreLineVolumes ? 1 : controlNode.Volume;

        if (controlNode.ControlType == ControlType.StartSliding)
        {
            if (_loopProviders.ShouldRemoveAll(controlNode.SlideChannel))
            {
                _loopProviders.RemoveAll(rootMixer);
            }

            try
            {
                _loopProviders.Create(controlNode, cachedSound, rootMixer, volume, 0, balanceFactor: 0);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error occurs while playing looped audio.", true);
            }
        }
        else if (controlNode.ControlType == ControlType.StopSliding)
        {
            _loopProviders.Remove(controlNode.SlideChannel, rootMixer);
        }
        else if (controlNode.ControlType == ControlType.ChangeVolume)
        {
            _loopProviders.ChangeAllVolumes(volume);
        }
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

            await InitializeNodeListsAsync(folder, beatmapFilename);
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
        var mixer = SharedViewModel.Instance.AudioEngine?.EffectMixer;
        _loopProviders.RemoveAll(mixer);
        _singleSynchronousTrack.ClearAudio();
        mixer?.RemoveAllMixerInputs();
        _playTime = 0;
        Combo = 0;

        if (_folder != null && OsuFile != null)
        {
            _ = _selectSongTrack.PlaySingleAudio(OsuFile, Path.Combine(_folder, OsuFile.General.AudioFilename ?? ""),
                OsuFile.General.PreviewTime);
        }
    }

    private void CleanAudioCaches()
    {
        _audioCacheService.ClearCaches();
    }

    private void ResetNodes()
    {
        GetCurrentAudioProvider().ResetNodes(PlayTime);
        _audioCacheService.PrecacheHitsoundsRangeInBackground(0, 13000, _keyList);
        _audioCacheService.PrecacheHitsoundsRangeInBackground(0, 13000, _playbackList);
        _nextCachingTime = 10000;
    }

    private async Task InitializeNodeListsAsync(string folder, string diffFilename)
    {
        _keyList.Clear();
        _playbackList.Clear();

        var osuDir = new OsuDirectory(folder);
        using (DebugUtils.CreateTimer("InitFolder", Logger))
        {
            await osuDir.InitializeAsync(diffFilename,
                ignoreWaveFiles: AppSettings.RealtimeOptions.IgnoreBeatmapHitsound);
        }

        if (osuDir.OsuFiles.Count <= 0)
        {
            Logger.Warn($"There is no available beatmaps after scanning. " +
                        $"Directory: {folder}; File: {diffFilename}");
            return;
        }

        var osuFile = osuDir.OsuFiles[0];
        OsuFile = osuFile;
        AudioFilename = osuFile.General?.AudioFilename;
        using var _ = DebugUtils.CreateTimer("InitAudio", Logger);
        var hitsoundList = await osuDir.GetHitsoundNodesAsync(osuFile);
        await Task.Delay(100);
        var isNightcore = PlayMods != Mods.Unknown && (PlayMods & Mods.Nightcore) != 0;
        if (isNightcore || AppSettings.RealtimeOptions.ForceNightcoreBeats)
        {
            if (isNightcore)
            {
                Logger.Info("Current Mods:" + PlayMods);
            }

            var list = NightcoreTilingHelper.GetHitsoundNodes(osuFile, TimeSpan.Zero);
            hitsoundList.AddRange(list);
            hitsoundList = hitsoundList.OrderBy(k => k.Offset).ToList();
        }

        GetCurrentAudioProvider().FillAudioList(hitsoundList, _keyList, _playbackList);
    }

    private void AddSkinCacheInBackground()
    {
        if (_folder == null)
        {
            Logger.Warn($"{nameof(_folder)} is null, stop adding cache.");
            return;
        }

        if (SharedViewModel.Instance.AudioEngine == null)
        {
            Logger.Warn($"{nameof(SharedViewModel.Instance.AudioEngine)} is null, stop adding cache.");
            return;
        }

        _audioCacheService.SetContext(_folder, AudioFilename);
        _audioCacheService.PrecacheMusicAndSkinInBackground();
    }

    private void AddAudioCacheInBackground(int startTime, int endTime,
        IEnumerable<HitsoundNode> playableNodes,
        [CallerArgumentExpression("playableNodes")] string? expression = null)
    {
        _audioCacheService.PrecacheHitsoundsRangeInBackground(startTime, endTime, playableNodes, expression);
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
        _selectSongTrack.StartLowPass(lower, upper);
    }

    internal bool TryGetCachedSound(string filenameWithoutExt, out CachedSound? cachedSound)
    {
        return _audioCacheService.TryGetCachedSound(filenameWithoutExt, out cachedSound);
    }

    internal void StopCurrentMusic(int fadeMs = 0)
    {
        _ = _selectSongTrack.StopCurrentMusic(fadeMs);
    }

    internal void SetResultFlag(bool value)
    {
        _result = value;
    }

    internal void SetSingleTrackPlayMods(Mods mods)
    {
        _singleSynchronousTrack.PlayMods = mods;
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
        _ = _selectSongTrack.PlaySingleAudio(osuFile, path!, playTime);
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
        _ = _selectSongTrack.PauseCurrentMusic();
    }

    internal void RecoverCurrentMusic()
    {
        _ = _selectSongTrack.RecoverCurrentMusic();
    }

    internal bool GetFirstStartInitialized() => _firstStartInitialized;
    internal void SetFirstStartInitialized(bool value) => _firstStartInitialized = value;

    internal void ClearMixerLoopsAndMainTrackAudio()
    {
        var mixer = SharedViewModel.Instance.AudioEngine?.EffectMixer;
        _loopProviders.RemoveAll(mixer);
        _singleSynchronousTrack.ClearAudio();
        mixer?.RemoveAllMixerInputs();
    }

    internal void ResetNodesExternal() => ResetNodes();

    internal string? GetMusicPath()
    {
        if (_folder == null || AudioFilename == null) return null;
        return Path.Combine(_folder, AudioFilename);
    }

    internal void SetMainTrackOffsetAndLeadIn(int offset, int leadInMs)
    {
        _singleSynchronousTrack.Offset = offset;
        _singleSynchronousTrack.LeadInMilliseconds = leadInMs;
    }

    internal bool IsResultFlag() => _result;

    internal void SyncMainTrackAudio(CachedSound sound, int positionMs)
    {
        _singleSynchronousTrack.SyncAudio(sound, positionMs);
    }

    internal void ClearMainTrackAudio()
    {
        _singleSynchronousTrack.ClearAudio();
    }

    internal void AdvanceCachingWindow(int newMs)
    {
        if (newMs > _nextCachingTime)
        {
            AddAudioCacheInBackground(_nextCachingTime, _nextCachingTime + 13000, _keyList);
            AddAudioCacheInBackground(_nextCachingTime, _nextCachingTime + 13000, _playbackList);
            _nextCachingTime += 10000;
        }
    }

    internal void PlayAutoPlaybackIfNeeded()
    {
        if (SharedViewModel.Instance.AutoMode || (PlayMods & Mods.Autoplay) != 0 || IsReplay)
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