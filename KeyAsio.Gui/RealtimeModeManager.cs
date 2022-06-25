using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Coosu.Beatmap;
using Coosu.Beatmap.Extensions;
using Coosu.Beatmap.Extensions.Playback;
using KeyAsio.Gui.Configuration;
using KeyAsio.Gui.Models;
using KeyAsio.Gui.Utils;
using Microsoft.Extensions.Logging;
using Milki.Extensions.MixPlayer.NAudioExtensions.Wave;
using NAudio.Wave.SampleProviders;
using OsuRTDataProvider.BeatmapInfo;
using OsuRTDataProvider.Listen;

namespace KeyAsio.Gui;

public class RealtimeModeManager : ViewModelBase
{
    private static readonly string[] SkinSounds = { "combobreak" };

    public static RealtimeModeManager Instance { get; } = new();
    private static readonly ILogger Logger = SharedUtils.GetLogger(nameof(RealtimeModeManager));

    private OsuListenerManager.OsuStatus _osuStatus;
    private int _playTime;
    private int _combo;
    private Beatmap? _beatmap;
    private bool _isStarted;

    private readonly object _isStartedLock = new();
    private readonly HitsoundFileCache _hitsoundFileCache = new();

    private readonly ConcurrentDictionary<PlayableNode, CachedSound?> _playNodeToCachedSoundMapping = new();
    private readonly ConcurrentDictionary<string, CachedSound?> _skinToCachedSoundMapping = new();

    private readonly List<PlayableNode> _hitList = new();
    private readonly List<PlayableNode> _playList = new();
    private Queue<PlayableNode> _hitQueue = new();
    private Queue<PlayableNode> _playQueue = new();

    private PlayableNode? _firstNode;
    private PlayableNode? _firstPlayNode;
    private string? _folder;
    private int _nextReadTime;

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
                this.RaiseAndSetIfChanged(ref _isStarted, value);
            }
        }
    }

    public OsuListenerManager? OsuListenerManager { get; set; }

    public AppSettings AppSettings => ConfigurationFactory.GetConfiguration<AppSettings>();

    public IEnumerable<PlaybackInfo> GetCurrentHitsounds()
    {
        int thresholdMs = 102; // determine by od
        using var _ = DebugUtils.CreateTimer($"GetSoundOnClick", Logger);
        var playTime = PlayTime;

        var audioPlaybackEngine = SharedViewModel.Instance.AudioPlaybackEngine;
        if (audioPlaybackEngine == null)
        {
            Logger.LogWarning($"Engine not ready, return empty.");
            return Array.Empty<PlaybackInfo>();
        }

        if (!IsStarted)
        {
            Logger.LogDebug($"Game hasn't started, return empty.");
            return Array.Empty<PlaybackInfo>();
        }

        var first = _firstNode;
        Logger.LogDebug($"Click at {playTime}, first node at {(first?.Offset.ToString() ?? "null")}");
        if (first == null)
        {
            Logger.LogDebug($"First is null, no item returns.");
            return Array.Empty<PlaybackInfo>();
        }

        if (playTime < first.Offset - thresholdMs)
        {
            Logger.LogDebug($"Haven't reached first, no item returns.");
            return Array.Empty<PlaybackInfo>();
        }

        if (playTime < first.Offset + thresholdMs) // click soon~0~late
        {
            return GetHitsoundList(false);
        }

        return GetHitsoundList(true);

        IEnumerable<PlaybackInfo> GetHitsoundList(bool checkPreTiming)
        {
            int counter = 0;
            bool isFirst = true;
            while (first != null)
            {
                if (!isFirst && !checkPreTiming && playTime < first.Offset - 3)
                {
                    //Logger.LogWarning($"Haven't reached first, return empty.");
                    break;
                }

                if (checkPreTiming && playTime >= first.Offset + thresholdMs)
                {
                    _hitQueue.TryDequeue(out first);
                    continue;
                }

                isFirst = false;
                checkPreTiming = false;
                if (_playNodeToCachedSoundMapping.TryGetValue(first, out var cachedSound) && cachedSound != null)
                {
                    counter++;
                    yield return new PlaybackInfo(cachedSound, first.Volume, first.Balance);
                }

                _hitQueue.TryDequeue(out first);
            }

            _firstNode = first;
            if (counter == 0)
            {
                Logger.LogWarning($"Counter is zero, no item returns.");
            }
        }
    }

    public IEnumerable<PlaybackInfo> GetPlaybackHitsounds(bool isAuto)
    {
        var playTime = PlayTime;

        var audioPlaybackEngine = SharedViewModel.Instance.AudioPlaybackEngine;
        if (audioPlaybackEngine == null)
        {
            return Array.Empty<PlaybackInfo>();
        }

        if (!IsStarted)
        {
            return Array.Empty<PlaybackInfo>();
        }

        var first = isAuto ? _firstPlayNode : _firstNode;
        if (first == null)
        {
            return Array.Empty<PlaybackInfo>();
        }

        if (playTime < first.Offset)
        {
            return Array.Empty<PlaybackInfo>();
        }

        return GetHitsoundList();

        IEnumerable<PlaybackInfo> GetHitsoundList()
        {
            while (first != null)
            {
                if (playTime < first.Offset)
                {
                    break;
                }

                if (_playNodeToCachedSoundMapping.TryGetValue(first, out var cachedSound) && cachedSound != null)
                {
                    yield return new PlaybackInfo(cachedSound, first.Volume, first.Balance);
                }

                if (isAuto)
                {
                    _playQueue.TryDequeue(out first);
                }
                else
                {
                    _hitQueue.TryDequeue(out first);
                }
            }

            if (isAuto)
            {
                _firstPlayNode = first;
            }
            else
            {
                _firstNode = first;
            }
        }
    }

    private void OnComboChanged(int oldCombo, int newCombo)
    {
        if (IsStarted && !AppSettings.RealtimeOptions.IgnoreComboBreak && newCombo < oldCombo && oldCombo >= 20)
        {
            if (_skinToCachedSoundMapping.TryGetValue("combobreak", out var cachedSound))
            {
                PlaySound(cachedSound, 1, 0);
            }
        }
    }

    private async void OnStatusChanged(OsuListenerManager.OsuStatus pre, OsuListenerManager.OsuStatus cur)
    {
        if (pre != OsuListenerManager.OsuStatus.Playing &&
            cur == OsuListenerManager.OsuStatus.Playing)
        {
            await StartAsync();
        }
        else
        {
            Stop();
        }
    }

    private void Stop()
    {
        Logger.LogDebug("Stop playing.");
        IsStarted = false;
        _playTime = 0;
        PlayTime = 0;
    }

    private async Task StartAsync()
    {
        // Load beatmap & hitsounds
        try
        {
            Logger.LogDebug("Start playing.");
            if (Beatmap == null)
            {
                throw new Exception("The beatmap is null!");
            }

            var folder = Path.GetDirectoryName(Beatmap.FilenameFull);
            if (_folder != folder)
            {
                Logger.LogDebug("Folder changed, cleaning caches.");
                CleanHitsoundCaches();
            }

            _folder = folder;
            if (folder == null)
            {
                throw new Exception("The beatmap folder is null!");
            }

            await InitSoundListAsync(folder, Beatmap.Filename);
            AddSkinCacheInBackground();
            RequeueNodes();
            IsStarted = true;
        }
        catch (Exception ex)
        {
            IsStarted = false;
            Logger.LogError(ex, "Error while starting a beatmap");
        }
    }

    private void AddSkinCacheInBackground()
    {
        Task.Run(() =>
        {
            SkinSounds.AsParallel()
                .WithDegreeOfParallelism(1)
                //.WithDegreeOfParallelism(Environment.ProcessorCount == 1 ? 1 : Environment.ProcessorCount / 2)
                .ForAll(skinSound =>
                {
                    AddSkinCache(skinSound).Wait();
                });
        });

    }

    private async Task InitSoundListAsync(string folder, string diffFilename)
    {
        _hitList.Clear();
        _playList.Clear();

        var osuDir = new OsuDirectory(folder);
        using (DebugUtils.CreateTimer("InitFolder", Logger))
        {
            await osuDir.InitializeAsync(diffFilename, ignoreWaveFiles: AppSettings.RealtimeOptions.IgnoreBeatmapHitsound);
        }

        if (osuDir.OsuFiles.Count <= 0)
        {
            Logger.LogWarning($"There is no available beatmaps after scanning. " +
                              $"Directory: {folder}; File: {diffFilename}");
            return;
        }

        using var _ = DebugUtils.CreateTimer("InitSound", Logger);
        var hitsoundList = await osuDir.GetHitsoundNodesAsync(osuDir.OsuFiles[0]);
        var secondaryCache = new List<PlayableNode>();

        foreach (var hitsoundNode in hitsoundList.OrderBy(k => k.Offset)) // Should be stable sort here
        {
            if (hitsoundNode is not PlayableNode playableNode) continue;

            if (playableNode.PlayablePriority == PlayablePriority.Primary)
            {
                CheckSecondary();
                secondaryCache.Clear();
                _hitList.Add(playableNode);
            }
            else if (playableNode.PlayablePriority is PlayablePriority.Secondary)
            {
                var sliderTailBehavior = AppSettings.RealtimeOptions.SliderTailPlaybackBehavior;
                if (sliderTailBehavior == SliderTailPlaybackBehavior.Normal)
                {
                    _playList.Add(playableNode);
                }
                else if (sliderTailBehavior == SliderTailPlaybackBehavior.KeepReverse)
                {
                    secondaryCache.Add(playableNode);
                }
            }
            else if (playableNode.PlayablePriority is PlayablePriority.Effects)
            {
                if (!AppSettings.RealtimeOptions.IgnoreSliderTicksAndSlides)
                {
                    _playList.Add(playableNode);
                }
            }
            else if (playableNode.PlayablePriority is PlayablePriority.Sampling)
            {
                if (!AppSettings.RealtimeOptions.IgnoreStoryboardSamples)
                {
                    _playList.Add(playableNode);
                }
            }
        }

        CheckSecondary();

        void CheckSecondary()
        {
            if (secondaryCache.Count <= 1) return;
            _playList.AddRange(secondaryCache);
        }
    }

    private void OnBeatmapChanged(Beatmap? beatmap)
    {
    }

    private void OnPlayTimeChanged(int oldMs, int newMs)
    {
        if (IsStarted && oldMs > newMs) // Retry
        {
            RequeueNodes();
            return;
        }

        if (IsStarted && newMs > _nextReadTime)
        {
            AddHitsoundCacheInBackground(_nextReadTime, _nextReadTime + 13000, _hitList);
            AddHitsoundCacheInBackground(_nextReadTime, _nextReadTime + 13000, _playList);
            _nextReadTime += 10000;
        }

        if (IsStarted && SharedViewModel.Instance.LatencyTestMode)
        {
            foreach (var playbackObject in GetPlaybackHitsounds(false))
            {
                PlaySound(playbackObject);
            }
        }

        if (IsStarted)
        {
            foreach (var playbackObject in GetPlaybackHitsounds(true))
            {
                PlaySound(playbackObject);
            }
        }
    }

    public void PlaySound(PlaybackInfo playbackObject)
    {
        PlaySound(playbackObject.CachedSound, playbackObject.Volume, playbackObject.Balance);
    }

    public void PlaySound(CachedSound cachedSound, float volume, float balance)
    {
        balance *= AppSettings.RealtimeOptions.BalanceFactor;
        SharedViewModel.Instance.AudioPlaybackEngine?.AddMixerInput(
            new Waves.BalanceSampleProvider(
                    new VolumeSampleProvider(new Waves.CachedSoundSampleProvider(cachedSound))
                    { Volume = volume }
                )
            { Balance = balance }
        );
        Logger.LogDebug($"Play {Path.GetFileNameWithoutExtension(cachedSound.SourcePath)}; " +
                        $"Vol. {volume}; " +
                        $"Bal. {balance}");
    }

    private void CleanHitsoundCaches()
    {
        CachedSoundFactory.ClearCacheSounds();
        _playNodeToCachedSoundMapping.Clear();
        _skinToCachedSoundMapping.Clear();
    }

    private void RequeueNodes()
    {
        _hitQueue = new Queue<PlayableNode>(_hitList);
        _playQueue = new Queue<PlayableNode>(_playList);
        _hitQueue.TryDequeue(out _firstNode);
        _playQueue.TryDequeue(out _firstPlayNode);
        AddHitsoundCacheInBackground(0, 13000, _hitList);
        AddHitsoundCacheInBackground(0, 13000, _playList);
        _nextReadTime = 10000;
    }

    private void AddHitsoundCacheInBackground(int startTime, int endTime, List<PlayableNode> playableNodes,
        [CallerArgumentExpression("playableNodes")]
        string? expression = null)
    {
        if (_folder == null)
        {
            Logger.LogWarning($"{nameof(_folder)} is null, stop adding cache.");
            return;
        }

        if (SharedViewModel.Instance.AudioPlaybackEngine == null)
        {
            Logger.LogWarning($"{nameof(SharedViewModel.Instance.AudioPlaybackEngine)} is null, stop adding cache.");
            return;
        }

        if (playableNodes.Count == 0)
        {
            Logger.LogWarning($"{expression} has no hitsounds, stop adding cache.");
            return;
        }

        var hitsoundList = playableNodes;
        var folder = _folder;
        var waveFormat = SharedViewModel.Instance.AudioPlaybackEngine.WaveFormat;
        var skinFolder = SharedViewModel.Instance.AppSettings?.SkinFolder ?? "";
        Task.Run(() =>
        {
            using var _ = DebugUtils.CreateTimer($"CacheSound {startTime}~{endTime}", Logger);
            hitsoundList
                .Where(k => k.Offset >= startTime && k.Offset < endTime)
                .AsParallel()
                .WithDegreeOfParallelism(1)
                //.WithDegreeOfParallelism(Environment.ProcessorCount == 1 ? 1 : Environment.ProcessorCount / 2)
                .ForAll(playableNode =>
                {
                    if (!IsStarted) return;
                    if (playableNode.Filename == null) return;

                    var path = Path.Combine(folder, playableNode.Filename);
                    string? identifier = null;
                    if (playableNode.UseUserSkin)
                    {
                        identifier = "internal";
                        var filename = _hitsoundFileCache.GetFileUntilFind(skinFolder, playableNode.Filename,
                            out var useUserSkin);
                        path = useUserSkin
                            ? Path.Combine(SharedViewModel.Instance.DefaultFolder, $"{playableNode.Filename}.ogg")
                            : Path.Combine(skinFolder, filename);
                    }

                    var (result, status) = CachedSoundFactory
                        .GetOrCreateCacheSoundStatus(waveFormat, path, identifier, checkFileExist: false).Result;

                    if (result == null)
                    {
                        Logger.LogWarning("Caching sound failed: " + path);
                    }
                    else if (status == true)
                    {
                        Logger.LogInformation("Cached sound: " + path);
                    }

                    _playNodeToCachedSoundMapping.TryAdd(playableNode, result);
                });
            //foreach (var playableNode in allHitsounds)
            //{
            //    if (!IsStarted) break;
            //    if (playableNode.Filename != null)
            //    {
            //        var path = Path.Combine(_folder!, playableNode.Filename);
            //        var identifier = playableNode.UseUserSkin ? "internal" : null;
            //        CachedSoundFactory.GetOrCreateCacheSound(waveFormat, path, identifier).Wait();
            //    }

            //    if (!IsStarted) break;
            //}
        });
    }

    private async Task<CachedSound?> AddSkinCache(string filenameWithoutExt)
    {
        if (SharedViewModel.Instance.AudioPlaybackEngine == null) return null;
        if (_folder == null) return null;

        if (_skinToCachedSoundMapping.TryGetValue(filenameWithoutExt, out var value)) return value;

        var waveFormat = SharedViewModel.Instance.AudioPlaybackEngine.WaveFormat;
        var skinFolder = SharedViewModel.Instance.AppSettings?.SkinFolder ?? "";

        string? identifier = null;
        var filename = _hitsoundFileCache.GetFileUntilFind(_folder, filenameWithoutExt, out var useUserSkin);
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
            path = Path.Combine(_folder, filename);
        }

        var (result, status) = await CachedSoundFactory
            .GetOrCreateCacheSoundStatus(waveFormat, path, identifier, checkFileExist: false);

        if (result == null)
        {
            Logger.LogWarning("Caching skin sound failed: " + path);
        }
        else if (status == true)
        {
            Logger.LogInformation("Cached skin sound: " + path);
        }

        _skinToCachedSoundMapping.TryAdd(filenameWithoutExt, result);

        return result;
    }
}