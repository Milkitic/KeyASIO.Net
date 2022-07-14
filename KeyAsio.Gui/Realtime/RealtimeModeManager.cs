using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Coosu.Beatmap;
using Coosu.Beatmap.Extensions;
using Coosu.Beatmap.Extensions.Playback;
using Coosu.Beatmap.Sections.GamePlay;
using KeyAsio.Gui.Configuration;
using KeyAsio.Gui.Models;
using KeyAsio.Gui.Realtime.AudioProviders;
using KeyAsio.Gui.Realtime.Tracks;
using KeyAsio.Gui.Utils;
using KeyAsio.Gui.Waves;
using Microsoft.Extensions.Logging;
using Milki.Extensions.MixPlayer.NAudioExtensions.Wave;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using OsuRTDataProvider.BeatmapInfo;
using OsuRTDataProvider.Listen;
using OsuRTDataProvider.Mods;

namespace KeyAsio.Gui.Realtime;

public class RealtimeModeManager : ViewModelBase
{
    private static readonly string[] SkinAudioFiles = { "combobreak" };

    public static RealtimeModeManager Instance { get; } = new();
    private static readonly ILogger Logger = LogUtils.GetLogger(nameof(RealtimeModeManager));

    private OsuListenerManager.OsuStatus _osuStatus;
    private int _playTime;
    private int _combo;
    private int _score;
    private Beatmap? _beatmap;
    private bool _isStarted;

    private readonly object _isStartedLock = new();
    private readonly HitsoundFileCache _hitsoundFileCache = new();

    private readonly ConcurrentDictionary<HitsoundNode, CachedSound?> _playNodeToCachedSoundMapping = new();
    private readonly ConcurrentDictionary<string, CachedSound?> _filenameToCachedSoundMapping = new();

    private readonly List<PlayableNode> _keyList = new();
    private readonly List<HitsoundNode> _playbackList = new();

    private readonly StandardAudioProvider _standardAudioProvider;
    private readonly ManiaAudioProvider _maniaAudioProvider;

    private readonly Dictionary<GameMode, IAudioProvider> _audioProviderDictionary;

    private readonly SingleSynchronousTrack _singleSynchronousTrack;
    private readonly SelectSongTrack _selectSongTrack;

    private readonly LoopProviders _loopProviders = new();

    private string? _folder;
    private string? _audioFilePath;

    private int _nextCachingTime;
    private ModsInfo.Mods _playMods;
    private bool _firstStartInitialized; // After starting a map and playtime to zero
    private bool _result;
    private string _username = "";

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

    public ModsInfo.Mods PlayMods
    {
        get => _playMods;
        set
        {
            if (value == _playMods) return;
            var val = _playMods;
            _playMods = value;
            OnPlayModsChanged(val, value);
            OnPropertyChanged();
        }
    }

    public int PlayTime
    {
        get => _playTime;
        set
        {
            value += AppSettings.RealtimeOptions.RealtimeModeAudioOffset;
            if (value == _playTime) return;
            var val = _playTime;
            _playTime = value;
            OnPlayTimeChanged(val, value);
            OnPropertyChanged();
        }
    }

    public int Combo
    {
        get => _combo;
        set
        {
            if (value == _combo) return;
            var val = _combo;
            _combo = value;
            OnComboChanged(val, value);
            OnPropertyChanged();
        }
    }

    public int Score
    {
        get => _score;
        set
        {
            if (value == _score) return;
            _score = value;
            OnPropertyChanged();
        }
    }

    public OsuListenerManager.OsuStatus OsuStatus
    {
        get => _osuStatus;
        set
        {
            if (value == _osuStatus) return;
            var val = _osuStatus;
            _osuStatus = value;
            OnStatusChanged(val, value);
            OnPropertyChanged();
        }
    }

    public OsuFile? OsuFile { get; /*private*/ set; }

    public string? AudioFilename { get; set; }

    public Beatmap? Beatmap
    {
        get => _beatmap;
        set
        {
            if (Equals(value, _beatmap)) return;
            _beatmap = value;
            OnBeatmapChanged(value);
            OnPropertyChanged();
        }
    }

    public bool IsStarted
    {
        get { lock (_isStartedLock) { return _isStarted; } }
        set { lock (_isStartedLock) { this.RaiseAndSetIfChanged(ref _isStarted, value); } }
    }

    public OsuListenerManager? OsuListenerManager { get; set; }

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
            Logger.DebuggingWarn("Fail to play: CachedSound not found");
            return;
        }

        if (AppSettings.RealtimeOptions.IgnoreLineVolumes)
        {
            volume = 1;
        }

        balance *= AppSettings.RealtimeOptions.BalanceFactor;
        SharedViewModel.Instance.AudioEngine?.EffectMixer.AddMixerInput(
            new Waves.BalanceSampleProvider(
                    new VolumeSampleProvider(new SeekableCachedSoundSampleProvider(cachedSound))
                    { Volume = volume }
                )
            { Balance = balance }
        );
        Logger.Debug($"Play {Path.GetFileNameWithoutExtension(cachedSound.SourcePath)}; " +
                     $"Vol. {volume}; " +
                     $"Bal. {balance}");
    }

    private void PlayLoopAudio(CachedSound? cachedSound, ControlNode controlNode)
    {
        var rootMixer = SharedViewModel.Instance.AudioEngine?.EffectMixer;
        if (rootMixer == null)
        {
            Logger.DebuggingWarn($"RootMixer is null, stop adding cache.");
            return;
        }

        var volume = AppSettings.RealtimeOptions.IgnoreLineVolumes ? 1 : controlNode.Volume;

        if (controlNode.ControlType == ControlType.StartSliding)
        {
            if (_loopProviders.ShouldRemoveAll(controlNode.SlideChannel))
            {
                _loopProviders.RemoveAll(rootMixer);
            }

            _loopProviders.Create(controlNode, cachedSound, rootMixer, volume, 0, balanceFactor: 0);
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
            Logger.DebuggingInfo("Start playing.");
            OsuFile = null;

            var folder = Path.GetDirectoryName(beatmapFilenameFull);
            if (_folder != folder)
            {
                Logger.DebuggingInfo("Cleaning caches caused by folder changing.");
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
            IsStarted = true;
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
        Logger.DebuggingInfo("Stop playing.");
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
            _selectSongTrack.PlaySingleAudio(OsuFile, Path.Combine(_folder, OsuFile.General.AudioFilename ?? ""),
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
            await osuDir.InitializeAsync(diffFilename, ignoreWaveFiles: AppSettings.RealtimeOptions.IgnoreBeatmapHitsound);
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
        if (PlayMods != ModsInfo.Mods.Unknown && (PlayMods & ModsInfo.Mods.Nightcore) != 0)
        {
            Logger.DebuggingInfo("Current Mods:" + PlayMods);
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
            Logger.DebuggingWarn($"{nameof(_folder)} is null, stop adding cache.");
            return;
        }

        if (SharedViewModel.Instance.AudioEngine == null)
        {
            Logger.DebuggingWarn($"{nameof(SharedViewModel.Instance.AudioEngine)} is null, stop adding cache.");
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
                    Logger.DebuggingInfo("Cached music: " + musicPath);
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
            Logger.DebuggingWarn($"{nameof(_folder)} is null, stop adding cache.");
            return;
        }

        if (SharedViewModel.Instance.AudioEngine == null)
        {
            Logger.DebuggingWarn($"{nameof(SharedViewModel.Instance.AudioEngine)} is null, stop adding cache.");
            return;
        }

        if (playableNodes is IList { Count: 0 })
        {
            Logger.DebuggingWarn($"{expression} has no hitsounds, stop adding cache.");
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
            Logger.DebuggingInfo("Cached skin audio: " + path);
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
            Logger.DebuggingWarn($"Isn't started, stop adding cache.");
            return;
        }

        if (hitsoundNode.Filename == null)
        {
            if (hitsoundNode is PlayableNode)
            {
                Logger.DebuggingWarn($"Filename is null, add null cache.");
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
            Logger.DebuggingInfo("Cached sound: " + path);
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

    private async void OnStatusChanged(OsuListenerManager.OsuStatus pre, OsuListenerManager.OsuStatus cur)
    {
        if (pre != OsuListenerManager.OsuStatus.Playing &&
            cur == OsuListenerManager.OsuStatus.Playing)
        {
            _selectSongTrack.StartLowPass(200, 800);
            _result = false;
            if (Beatmap == null)
            {
                Logger.Warn("Failed to start: the beatmap is null");
            }
            else
            {
                await StartAsync(Beatmap.FilenameFull, Beatmap.Filename);
            }
        }
        else if (pre == OsuListenerManager.OsuStatus.Playing && cur == OsuListenerManager.OsuStatus.Rank)
        {
            _result = true;
            _singleSynchronousTrack.PlayMods = ModsInfo.Mods.None;
        }
        else
        {
            _selectSongTrack.StartLowPass(200, 16000);
            _result = false;
            Stop();
        }
    }

    private void OnBeatmapChanged(Beatmap? beatmap)
    {
        if (OsuStatus == OsuListenerManager.OsuStatus.SelectSong && beatmap != null)
        {
            var coosu = OsuFile.ReadFromFile(beatmap.FilenameFull, k =>
            {
                k.IncludeSection("General");
                k.IncludeSection("Metadata");
            });
            var audioFilePath = coosu.General.AudioFilename == null
                ? null
                : Path.Combine(beatmap.Folder, coosu.General.AudioFilename);
            if (audioFilePath == _audioFilePath)
            {
                return;
            }

            _folder = beatmap.Folder;
            _audioFilePath = audioFilePath;
            _selectSongTrack.StopCurrentMusic(200);
            _selectSongTrack.PlaySingleAudio(coosu, audioFilePath, coosu.General.PreviewTime);
        }
    }

    private void OnPlayModsChanged(ModsInfo.Mods oldMods, ModsInfo.Mods newMods)
    {
    }

    private void OnPlayTimeChanged(int oldMs, int newMs)
    {
        if (IsStarted && oldMs > newMs) // Retry
        {
            _selectSongTrack.StopCurrentMusic();
            _selectSongTrack.StartLowPass(200, 16000);
            _firstStartInitialized = true;
            var mixer = SharedViewModel.Instance.AudioEngine?.EffectMixer;
            _loopProviders.RemoveAll(mixer);
            mixer?.RemoveAllMixerInputs();
            _singleSynchronousTrack.ClearAudio();

            ResetNodes();
            return;
        }

        if (IsStarted && AppSettings.RealtimeOptions.EnableMusicFunctions)
        {
            if (_firstStartInitialized && OsuFile != null && AudioFilename != null && _folder != null && SharedViewModel.Instance.AudioEngine != null)
            {
                var musicPath = Path.Combine(_folder, AudioFilename);
                if (CachedSoundFactory.ContainsCache(musicPath))
                {
                    //todo: online offset && local offset
                    const int codeLatency = -1;
                    const int osuForceLatency = 15;
                    var oldMapForceOffset = OsuFile.Version < 5 ? 24 : 0;
                    _singleSynchronousTrack.Offset = osuForceLatency + codeLatency + oldMapForceOffset;
                    _singleSynchronousTrack.LeadInMilliseconds = OsuFile.General.AudioLeadIn;
                    if (!_result)
                    {
                        _singleSynchronousTrack.PlayMods = PlayMods;
                    }

                    _singleSynchronousTrack.SyncAudio(CachedSoundFactory.GetCacheSound(musicPath), newMs);
                }
            }
        }

        if (IsStarted && newMs > _nextCachingTime)
        {
            AddAudioCacheInBackground(_nextCachingTime, _nextCachingTime + 13000, _keyList);
            AddAudioCacheInBackground(_nextCachingTime, _nextCachingTime + 13000, _playbackList);
            _nextCachingTime += 10000;
        }

        if (IsStarted && SharedViewModel.Instance.LatencyTestMode)
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