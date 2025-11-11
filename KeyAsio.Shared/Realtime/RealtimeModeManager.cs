using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using Coosu.Beatmap;
using Coosu.Beatmap.Extensions;
using Coosu.Beatmap.Extensions.Playback;
using Coosu.Beatmap.Sections.GamePlay;
using KeyAsio.MemoryReading;
using KeyAsio.MemoryReading.Logging;
using KeyAsio.Shared.Audio;
using KeyAsio.Shared.Models;
using KeyAsio.Shared.Realtime.AudioProviders;
using KeyAsio.Shared.Realtime.States;
using KeyAsio.Shared.Realtime.Tracks;
using KeyAsio.Shared.Utils;
using Milki.Extensions.Configuration;
using Milki.Extensions.MixPlayer.NAudioExtensions.Wave;
using NAudio.Wave;
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
    private readonly HitsoundFileCache _hitsoundFileCache = new();

    private readonly ConcurrentDictionary<HitsoundNode, CachedSound?> _playNodeToCachedSoundMapping = new();
    private readonly ConcurrentDictionary<string, CachedSound?> _filenameToCachedSoundMapping = new();

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
    }

    public string? Username
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            if (!string.IsNullOrEmpty(value))
            {
                AppSettings.PlayerBase64 = EncodeUtils.GetBase64String(value, Encoding.ASCII);
            }

            OnPropertyChanged();
        }
    }

    public Mods PlayMods
    {
        get;
        set
        {
            var val = field;
            if (SetField(ref field, value))
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
        get;
        set
        {
            if (SetField(ref field, value))
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
        get;
        set
        {
            var val = field;
            if (SetField(ref field, value))
            {
                OnComboChanged(val, value);
            }
        }
    }

    public int Score
    {
        get;
        set => SetField(ref field, value);
    }

    public bool IsReplay { get; set; }

    public OsuMemoryStatus OsuStatus
    {
        get;
        set
        {
            if (SetField(ref field, value))
            {
                _ = OnStatusChanged(field);
            }
        }
    }

    public OsuFile? OsuFile { get; internal set; }

    public string? AudioFilename { get; set; }

    public BeatmapIdentifier Beatmap
    {
        get;
        set
        {
            if (SetField(ref field, value))
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
                return field;
            }
        }
        set
        {
            lock (_isStartedLock)
            {
                SetField(ref field, value);
            }
        }
    }

    public AppSettings AppSettings => ConfigurationFactory.GetConfiguration<AppSettings>();
    public IReadOnlyList<HitsoundNode> PlaybackList => _playbackList;
    public List<PlayableNode> KeyList => _keyList;

    public bool TryGetAudioByNode(HitsoundNode playableNode, out CachedSound? cachedSound)
    {
        if (!_playNodeToCachedSoundMapping.TryGetValue(playableNode, out cachedSound)) return false;
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
        CachedSoundFactory.ClearCacheSounds();
        _playNodeToCachedSoundMapping.Clear();
        _filenameToCachedSoundMapping.Clear();
    }

    private void ResetNodes()
    {
        GetCurrentAudioProvider().ResetNodes(PlayTime);
        AddAudioCacheInBackground(0, 13000, _keyList);
        AddAudioCacheInBackground(0, 13000, _playbackList);
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

        var folder = _folder;
        var waveFormat = SharedViewModel.Instance.AudioEngine.WaveFormat;
        var skinFolder = SharedViewModel.Instance.SelectedSkin?.Folder ?? "";
        Task.Run(() =>
        {
            if (folder != null && AudioFilename != null)
            {
                var musicPath = Path.Combine(folder, AudioFilename);
                var (result, status) = CachedSoundFactory.GetOrCreateCacheSoundStatus(waveFormat, musicPath).Result;

                if (result == null)
                {
                    Logger.Warn("Caching sound failed: " + (File.Exists(musicPath) ? musicPath : "FileNotFound"));
                }
                else if (status == true)
                {
                    Logger.Info("Cached music: " + musicPath);
                }
            }

            SkinAudioFiles.AsParallel()
                .WithDegreeOfParallelism(1)
                //.WithDegreeOfParallelism(Environment.ProcessorCount == 1 ? 1 : Environment.ProcessorCount / 2)
                .ForAll(skinSound =>
                {
                    AddSkinCache(skinSound, folder, skinFolder, waveFormat).Wait();
                });
        });
    }

    private void AddAudioCacheInBackground(int startTime, int endTime,
        IEnumerable<HitsoundNode> playableNodes,
        [CallerArgumentExpression("playableNodes")]
        string? expression = null)
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

        if (playableNodes is IList { Count: 0 })
        {
            Logger.Warn($"{expression} has no hitsounds, stop adding cache.");
            return;
        }

        var hitsoundList = playableNodes;
        var folder = _folder;
        var waveFormat = SharedViewModel.Instance.AudioEngine.WaveFormat;
        var skinFolder = SharedViewModel.Instance.SelectedSkin?.Folder ?? "";
        Task.Run(() =>
        {
            using var _ = DebugUtils.CreateTimer($"CacheAudio {startTime}~{endTime}", Logger);
            hitsoundList
                .Where(k => k.Offset >= startTime && k.Offset < endTime)
                .AsParallel()
                .WithDegreeOfParallelism(1)
                //.WithDegreeOfParallelism(Environment.ProcessorCount == 1 ? 1 : Environment.ProcessorCount / 2)
                .ForAll(playableNode =>
                {
                    AddHitsoundCache(playableNode, folder, skinFolder, waveFormat).Wait();
                });
        });
    }

    private async Task<CachedSound?> AddSkinCache(string filenameWithoutExt,
        string beatmapFolder,
        string skinFolder,
        WaveFormat waveFormat)
    {
        if (_filenameToCachedSoundMapping.TryGetValue(filenameWithoutExt, out var value)) return value;

        string? identifier = null;
        var filename = _hitsoundFileCache.GetFileUntilFind(beatmapFolder, filenameWithoutExt, out var useUserSkin);
        string path;
        if (useUserSkin)
        {
            identifier = "internal";
            filename = _hitsoundFileCache.GetFileUntilFind(skinFolder, filenameWithoutExt,
                out useUserSkin);
            path = useUserSkin
                ? Path.Combine(SharedViewModel.Instance.DefaultFolder, $"{filenameWithoutExt}.ogg")
                : Path.Combine(skinFolder, filename);
        }
        else
        {
            path = Path.Combine(beatmapFolder, filename);
        }

        var (result, status) = await CachedSoundFactory
            .GetOrCreateCacheSoundStatus(waveFormat, path, identifier, checkFileExist: false);

        if (result == null)
        {
            Logger.Warn("Caching sound failed: " + (File.Exists(path) ? path : "FileNotFound"));
        }
        else if (status == true)
        {
            Logger.Info("Cached skin audio: " + path);
        }

        _filenameToCachedSoundMapping.TryAdd(filenameWithoutExt, result);

        return result;
    }

    private async Task AddHitsoundCache(HitsoundNode hitsoundNode,
        string beatmapFolder,
        string skinFolder,
        WaveFormat waveFormat)
    {
        if (!IsStarted)
        {
            Logger.Warn($"Isn't started, stop adding cache.");
            return;
        }

        if (hitsoundNode.Filename == null)
        {
            if (hitsoundNode is PlayableNode)
            {
                Logger.Warn($"Filename is null, add null cache.");
            }

            _playNodeToCachedSoundMapping.TryAdd(hitsoundNode, null);
            return;
        }

        var path = Path.Combine(beatmapFolder, hitsoundNode.Filename);
        string? identifier = null;
        if (hitsoundNode.UseUserSkin)
        {
            identifier = "internal";
            var filename = _hitsoundFileCache.GetFileUntilFind(skinFolder, hitsoundNode.Filename,
                out var useUserSkin);
            path = useUserSkin
                ? Path.Combine(SharedViewModel.Instance.DefaultFolder, $"{hitsoundNode.Filename}.ogg")
                : Path.Combine(skinFolder, filename);
        }

        var (result, status) = await CachedSoundFactory
            .GetOrCreateCacheSoundStatus(waveFormat, path, identifier, checkFileExist: false);

        if (result == null)
        {
            Logger.Warn("Caching sound failed: " + (File.Exists(path) ? path : "FileNotFound"));
        }
        else if (status == true)
        {
            Logger.Info("Cached sound: " + path);
        }

        _playNodeToCachedSoundMapping.TryAdd(hitsoundNode, result);
        _filenameToCachedSoundMapping.TryAdd(Path.GetFileNameWithoutExtension(path), result);
    }

    private IAudioProvider GetCurrentAudioProvider()
    {
        if (OsuFile == null) return _standardAudioProvider;
        return _audioProviderDictionary[OsuFile.General.Mode];
    }

    private void OnComboChanged(int oldCombo, int newCombo)
    {
        if (IsStarted && !AppSettings.RealtimeOptions.IgnoreComboBreak &&
            newCombo < oldCombo && oldCombo >= 20 &&
            Score != 0)
        {
            if (_filenameToCachedSoundMapping.TryGetValue("combobreak", out var cachedSound))
            {
                PlayAudio(cachedSound, 1, 0);
            }
        }
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
    }

    private void OnFetchedPlayTimeChanged(int oldMs, int newMs, bool paused = false)
    {
        _stateMachine.Current?.OnPlayTimeChanged(this, oldMs, newMs, paused);
    }

    // Internal helpers for state classes
    internal void StartLowPass(int lower, int upper)
    {
        _selectSongTrack.StartLowPass(lower, upper);
    }

    internal void StopCurrentMusic(int fadeMs = 0)
    {
        _selectSongTrack.StopCurrentMusic(fadeMs);
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

    // New helpers for playtime migration
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