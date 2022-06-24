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
    public static RealtimeModeManager Instance { get; } = new();
    private static readonly ILogger Logger = SharedUtils.GetLogger(nameof(RealtimeModeManager));

    private OsuListenerManager.OsuStatus _osuStatus;
    private int _playTime;
    private Beatmap? _beatmap;
    private bool _isStarted;

    private readonly object _isStartedLock = new();
    private readonly HitsoundFileCache _hitsoundFileCache = new();

    private readonly ConcurrentDictionary<PlayableNode, CachedSound?> _playNodeToCachedSoundMapping = new();
    private Queue<PlayableNode> _hitQueue = new();
    private Queue<PlayableNode> _secondaryHitQueue = new();

    private PlayableNode? _firstNode;
    private PlayableNode? _firstAutoNode;
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

    public List<PlayableNode> HitsoundList { get; } = new();

    public List<PlayableNode> AutoList { get; } = new();

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
        int thresholdMs = 70; // determine by od
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
            Logger.LogWarning($"Game hasn't started, return empty.");
            return Array.Empty<PlaybackInfo>();
        }

        var first = _firstNode;
        Logger.LogDebug($"Click at {playTime}, first node at {(first?.Offset.ToString() ?? "null")}");
        if (first == null)
        {
            Logger.LogWarning($"First is null, return empty.");
            return Array.Empty<PlaybackInfo>();
        }

        if (playTime < first.Offset - thresholdMs)
        {
            Logger.LogWarning($"Haven't reached first, return empty.");
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
            while (first != null)
            {
                if (!checkPreTiming && playTime < first.Offset - thresholdMs)
                {
                    Logger.LogWarning($"Haven't reached first, return empty.");
                    break;
                }

                if (checkPreTiming && playTime >= first.Offset + thresholdMs)
                {
                    _hitQueue.TryDequeue(out first);
                    continue;
                }

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
                Logger.LogWarning($"List is empty!!");
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

        var first = isAuto ? _firstAutoNode : _firstNode;
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
                    _secondaryHitQueue.TryDequeue(out first);
                }
                else
                {
                    _hitQueue.TryDequeue(out first);
                }
            }

            if (isAuto)
            {
                _firstAutoNode = first;
            }
            else
            {
                _firstNode = first;
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

            RequeueNodes();
            IsStarted = true;
        }
        catch (Exception ex)
        {
            IsStarted = false;
            Logger.LogError(ex, "Error while starting a beatmap");
        }
    }

    private async Task InitSoundListAsync(string folder, string diffFilename)
    {
        HitsoundList.Clear();
        AutoList.Clear();

        var osuDir = new OsuDirectory(folder);
        using (DebugUtils.CreateTimer("InitFolder", Logger))
        {
            await osuDir.InitializeAsync(diffFilename);
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
                AutoList.Clear();
                HitsoundList.Add(playableNode);
            }
            else if (playableNode.PlayablePriority is PlayablePriority.Secondary)
            {
                var sliderTailBehavior = AppSettings.RealtimeOptions.SliderTailPlaybackBehavior;
                if (sliderTailBehavior == SliderTailPlaybackBehavior.Normal)
                {
                    AutoList.Add(playableNode);
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
                    AutoList.Add(playableNode);
                }
            }
            else if (playableNode.PlayablePriority is PlayablePriority.Sampling)
            {
                if (!AppSettings.RealtimeOptions.IgnoreStoryboardSamples)
                {
                    AutoList.Add(playableNode);
                }
            }
        }

        CheckSecondary();

        void CheckSecondary()
        {
            if (secondaryCache.Count <= 1) return;
            AutoList.AddRange(secondaryCache);
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
            AddHitsoundCacheInBackground(_nextReadTime, _nextReadTime + 13000, HitsoundList);
            AddHitsoundCacheInBackground(_nextReadTime, _nextReadTime + 13000, AutoList);
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
        SharedViewModel.Instance.AudioPlaybackEngine?.AddMixerInput(new Waves.BalanceSampleProvider(
                new VolumeSampleProvider(
                        new Waves.CachedSoundSampleProvider(playbackObject.CachedSound))
                { Volume = playbackObject.Volume }
            )
        { Balance = playbackObject.Balance * AppSettings.RealtimeOptions.BalanceFactor }
        );
        Logger.LogDebug($"Play {Path.GetFileNameWithoutExtension(playbackObject.CachedSound.SourcePath)}; " +
                        $"Vol. {playbackObject.Volume}; " +
                        $"Bal. {playbackObject.Balance}");
    }

    private void CleanHitsoundCaches()
    {
        CachedSoundFactory.ClearCacheSounds();
        _playNodeToCachedSoundMapping.Clear();
    }

    private void RequeueNodes()
    {
        _hitQueue = new Queue<PlayableNode>(HitsoundList);
        _secondaryHitQueue = new Queue<PlayableNode>(AutoList);
        _firstNode = _hitQueue.Dequeue();
        _firstAutoNode = _hitQueue.Dequeue();
        AddHitsoundCacheInBackground(0, 13000, HitsoundList);
        AddHitsoundCacheInBackground(0, 13000, AutoList);
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
                .WithDegreeOfParallelism(Environment.ProcessorCount == 1 ? 1 : Environment.ProcessorCount / 2)
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
}