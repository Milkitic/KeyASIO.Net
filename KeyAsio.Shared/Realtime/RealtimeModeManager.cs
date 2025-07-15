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
using KeyAsio.Shared.Realtime.Managers;
using KeyAsio.Shared.Realtime.Tracks;
using KeyAsio.Shared.Utils;
using Milki.Extensions.Configuration;
using Milki.Extensions.MixPlayer.NAudioExtensions.Wave;
using NAudio.Wave;
using OsuMemoryDataProvider;
using BalanceSampleProvider = KeyAsio.Shared.Audio.BalanceSampleProvider;

namespace KeyAsio.Shared.Realtime;

/// <summary>
/// 实时模式管理器，专注于音效播放的核心功能
/// </summary>
public class RealtimeModeManager : ViewModelBase
{
    public static RealtimeModeManager Instance { get; } = new();
    private static readonly ILogger Logger = LogUtils.GetLogger(nameof(RealtimeModeManager));

    private int _combo;
    private int _score;
    private bool _isStarted;
    private string _username = "";
    private Mods _playMods;
    private int _nextCachingTime;

    private readonly object _isStartedLock = new();
    private readonly List<PlayableNode> _keyList = new();
    private readonly List<HitsoundNode> _playbackList = new();

    private readonly StandardAudioProvider _standardAudioProvider;
    private readonly ManiaAudioProvider _maniaAudioProvider;
    private readonly Dictionary<GameMode, IAudioProvider> _audioProviderDictionary;
    private readonly LoopProviders _loopProviders = new();

    // 拆分出的管理器
    private readonly GameStateManager _gameStateManager;
    private readonly BeatmapManager _beatmapManager;
    private readonly AudioCacheManager _audioCacheManager;
    private readonly TimeSyncManager _timeSyncManager;
    private readonly SingleSynchronousTrack _singleSynchronousTrack;
    private readonly SelectSongTrack _selectSongTrack;

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

        // 初始化管理器
        _gameStateManager = new GameStateManager(_selectSongTrack);
        _beatmapManager = new BeatmapManager(_selectSongTrack);
        _audioCacheManager = new AudioCacheManager();
        _timeSyncManager = new TimeSyncManager(_singleSynchronousTrack);

        // 设置事件处理
        _gameStateManager.GameStartRequested += OnGameStartRequested;
        _gameStateManager.GameStopRequested += OnGameStopRequested;
        _timeSyncManager.PlayTimeChanged += OnPlayTimeChanged;
        _timeSyncManager.RetryDetected += OnRetryDetected;

        Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.AboveNormal;
    }

    public string Username
    {
        get => _username;
        set
        {
            if (value == _username) return;
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
        get => _timeSyncManager.PlayTime;
        set => _timeSyncManager.PlayTime = value;
    }

    public int LastFetchedPlayTime
    {
        get => _timeSyncManager.LastFetchedPlayTime;
        set => _timeSyncManager.LastFetchedPlayTime = value;
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
        get => _gameStateManager.OsuStatus;
        set => _gameStateManager.OsuStatus = value;
    }

    public OsuFile? OsuFile 
    { 
        get => _beatmapManager.OsuFile;
        internal set => _beatmapManager.OsuFile = value;
    }

    public string? AudioFilename 
    { 
        get => _beatmapManager.AudioFilename;
        set => _beatmapManager.AudioFilename = value;
    }

    public BeatmapIdentifier Beatmap
    {
        get => _beatmapManager.Beatmap;
        set => _beatmapManager.Beatmap = value;
    }

    public bool IsStarted
    {
        get { lock (_isStartedLock) { return _isStarted; } }
        set { lock (_isStartedLock) { SetField(ref _isStarted, value); } }
    }

    public AppSettings AppSettings => ConfigurationFactory.GetConfiguration<AppSettings>();

    public IReadOnlyList<HitsoundNode> PlaybackList => _playbackList;
    public List<PlayableNode> KeyList => _keyList;

    public bool TryGetAudioByNode(HitsoundNode playableNode, out CachedSound? cachedSound)
    {
        return _audioCacheManager.TryGetAudioByNode(playableNode, out cachedSound);
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
            if (_beatmapManager.Folder != folder)
            {
                Logger.Info("Cleaning caches caused by folder changing.");
                _audioCacheManager.CleanAudioCaches();
            }

            _beatmapManager.Folder = folder;
            if (folder == null)
            {
                throw new Exception("The beatmap folder is null!");
            }

            var result = await _beatmapManager.InitializeNodeListsAsync(folder, beatmapFilename, PlayMods);
            _keyList.Clear();
            _playbackList.Clear();
            _keyList.AddRange(result.KeyList);
            _playbackList.AddRange(result.PlaybackList);

            if (result.OsuFile != null && result.HitsoundList != null)
            {
                GetCurrentAudioProvider().FillAudioList(result.HitsoundList, _keyList, _playbackList);
            }

            _audioCacheManager.AddSkinCacheInBackground(folder, AudioFilename);
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
        _timeSyncManager.FirstStartInitialized = false;
        var mixer = SharedViewModel.Instance.AudioEngine?.EffectMixer;
        _loopProviders.RemoveAll(mixer);
        _singleSynchronousTrack.ClearAudio();
        mixer?.RemoveAllMixerInputs();
        _timeSyncManager.Reset();
        Combo = 0;

        if (_beatmapManager.Folder != null && OsuFile != null)
        {
            _selectSongTrack.PlaySingleAudio(OsuFile, Path.Combine(_beatmapManager.Folder, OsuFile.General.AudioFilename ?? ""),
                OsuFile.General.PreviewTime);
        }
    }

    private void ResetNodes()
    {
        GetCurrentAudioProvider().ResetNodes(PlayTime);
        _audioCacheManager.AddAudioCacheInBackground(0, 13000, _keyList, _beatmapManager.Folder, IsStarted);
        _audioCacheManager.AddAudioCacheInBackground(0, 13000, _playbackList, _beatmapManager.Folder, IsStarted);
        _nextCachingTime = 10000;
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
            if (_audioCacheManager.TryGetAudioByFilename("combobreak", out var cachedSound))
            {
                PlayAudio(cachedSound, 1, 0);
            }
        }
    }

    private void OnPlayModsChanged(Mods oldMods, Mods newMods)
    {
        // Mods 变化处理逻辑
    }

    // 事件处理方法
    private async void OnGameStartRequested(object? sender, EventArgs e)
    {
        if (Beatmap == null)
        {
            Logger.Warn("Failed to start: the beatmap is null");
        }
        else
        {
            await StartAsync(Beatmap.FilenameFull, Beatmap.Filename);
        }
    }

    private void OnGameStopRequested(object? sender, EventArgs e)
    {
        Stop();
    }

    private void OnRetryDetected(object? sender, EventArgs e)
    {
        _gameStateManager.ResetPauseCount();
        _selectSongTrack.StopCurrentMusic();
        _selectSongTrack.StartLowPass(200, 16000);
        _timeSyncManager.FirstStartInitialized = true;
        var mixer = SharedViewModel.Instance.AudioEngine?.EffectMixer;
        _loopProviders.RemoveAll(mixer);
        mixer?.RemoveAllMixerInputs();
        _singleSynchronousTrack.ClearAudio();

        ResetNodes();
    }

    private void OnPlayTimeChanged(object? sender, PlayTimeChangedEventArgs e)
    {
        _gameStateManager.HandlePlayTimePause(e.IsPaused);

        _timeSyncManager.HandlePlayTimeChange(e.OldTime, e.NewTime, e.IsPaused, IsStarted, 
            OsuFile, AudioFilename, _beatmapManager.Folder, PlayMods, _gameStateManager.Result);

        if (IsStarted && e.NewTime > _nextCachingTime)
        {
            _audioCacheManager.AddAudioCacheInBackground(_nextCachingTime, _nextCachingTime + 13000, _keyList, _beatmapManager.Folder, IsStarted);
            _audioCacheManager.AddAudioCacheInBackground(_nextCachingTime, _nextCachingTime + 13000, _playbackList, _beatmapManager.Folder, IsStarted);
            _nextCachingTime += 10000;
        }

        if (IsStarted && (SharedViewModel.Instance.AutoMode || (PlayMods & Mods.Autoplay) != 0 || IsReplay))
        {
            foreach (var playbackObject in GetPlaybackAudio(false))
            {
                PlayAudio(playbackObject);
            }
        }

        if (IsStarted)
        {
            foreach (var playbackObject in GetPlaybackAudio(true))
            {
                PlayAudio(playbackObject);
            }
        }
    }
}