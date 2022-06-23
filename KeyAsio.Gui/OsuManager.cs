using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Coosu.Beatmap;
using Coosu.Beatmap.Extensions;
using Coosu.Beatmap.Extensions.Playback;
using KeyAsio.Gui.Utils;
using Microsoft.Extensions.Logging;
using Milki.Extensions.MixPlayer.NAudioExtensions.Wave;
using OsuRTDataProvider.BeatmapInfo;
using OsuRTDataProvider.Listen;

namespace KeyAsio.Gui;

public class PlaybackObject
{
    public PlaybackObject(CachedSound cachedSound, float volume, float balance)
    {
        CachedSound = cachedSound;
        Volume = volume;
        Balance = balance;
    }

    public CachedSound CachedSound { get; }
    public float Volume { get; }
    public float Balance { get; }
}

public class OsuManager : ViewModelBase
{
    public static OsuManager Instance { get; } = new();
    private static readonly ILogger Logger = SharedUtils.GetLogger(nameof(OsuManager));

    private OsuListenerManager.OsuStatus _osuStatus;
    private int _playTime;
    private Beatmap? _beatmap;
    private bool _isStarted;
    private List<PlayableNode>? _hitsoundList;

    private readonly object _isStartedLock = new();
    private readonly HitsoundFileCache _hitsoundFileCache = new();
    private readonly ConcurrentDictionary<PlayableNode, CachedSound?> _playNodeToCachedSoundMapping = new();
    private Queue<PlayableNode> _hitQueue = new();
    private PlayableNode? _firstNode;
    private string? _folder;
    private int _nextReadTime;

    public int PlayTime
    {
        get => _playTime;
        set
        {
            value += (SharedViewModel.Instance.AppSettings?.OsuModeAudioOffset ?? 0);
            if (value == _playTime) return;
            var val = _playTime;
            _playTime = value;
            OsuModeManager_OnPlayTimeChanged(val, value);
            OnPropertyChanged();
        }
    }

    public ObservableCollection<int> PlayTimeList { get; } = new();

    public OsuListenerManager.OsuStatus OsuStatus
    {
        get => _osuStatus;
        set
        {
            if (value == _osuStatus) return;
            var val = _osuStatus;
            _osuStatus = value;
            OsuModeManager_OnStatusChanged(val, value);
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
            OsuModeManager_OnBeatmapChanged(value);
            OnPropertyChanged();
        }
    }

    public List<PlayableNode>? HitsoundList
    {
        get => _hitsoundList;
        set => this.RaiseAndSetIfChanged(ref _hitsoundList, value);
    }

    public bool IsStarted
    {
        get { lock (_isStartedLock) { return _isStarted; } }
        set { lock (_isStartedLock) { this.RaiseAndSetIfChanged(ref _isStarted, value); } }
    }

    public OsuListenerManager? OsuListenerManager { get; set; }

    public IReadOnlyCollection<PlaybackObject> GetCurrentHitsounds(int thresholdMs = 30)
    {
        using var _ = DebugUtils.CreateTimer($"GetSound", Logger);
        var playTime = PlayTime;

        var audioPlaybackEngine = SharedViewModel.Instance.AudioPlaybackEngine;
        if (audioPlaybackEngine == null) return Array.Empty<PlaybackObject>();

        if (!IsStarted)
        {
            return Array.Empty<PlaybackObject>();
        }

        var first = _firstNode;
        Logger.LogDebug($"Click at {playTime}, first node at {(first?.Offset.ToString() ?? "null")}");
        if (first == null)
        {
            return Array.Empty<PlaybackObject>();
        }

        if (playTime < first.Offset - thresholdMs)
        {
            return Array.Empty<PlaybackObject>();
        }

        if (playTime < first.Offset + thresholdMs)
        {
            return GetHitsoundList(false);
        }

        return GetHitsoundList(true);

        IReadOnlyCollection<PlaybackObject> GetHitsoundList(bool checkPreTiming)
        {
            var list = new List<PlaybackObject>();
            bool skipChecking = !checkPreTiming;
            while (first != null)
            {
                if (skipChecking && playTime < first.Offset + thresholdMs)
                {
                    break;
                }

                if (!skipChecking && playTime >= first.Offset + thresholdMs)
                {
                    _hitQueue.TryDequeue(out first);
                    continue;
                }

                skipChecking = true;
                if (_playNodeToCachedSoundMapping.TryGetValue(first, out var cachedSound) && cachedSound != null)
                {
                    list.Add(new PlaybackObject(cachedSound, first.Volume, first.Balance));
                }

                _hitQueue.TryDequeue(out first);
            }

            _firstNode = first;
            return list;
        }
    }

    public IEnumerable<PlaybackObject> GetPlaybackHitsounds()
    {
        using var _ = DebugUtils.CreateTimer($"GetSound", Logger);
        var playTime = PlayTime;

        var audioPlaybackEngine = SharedViewModel.Instance.AudioPlaybackEngine;
        if (audioPlaybackEngine == null) return Array.Empty<PlaybackObject>();

        if (!IsStarted)
        {
            return Array.Empty<PlaybackObject>();
        }

        var first = _firstNode;
        if (first == null)
        {
            return Array.Empty<PlaybackObject>();
        }

        if (playTime < first.Offset)
        {
            return Array.Empty<PlaybackObject>();
        }

        return GetHitsoundList();

        IEnumerable<PlaybackObject> GetHitsoundList()
        {
            while (first != null)
            {
                if (playTime < first.Offset)
                {
                    break;
                }

                if (_playNodeToCachedSoundMapping.TryGetValue(first, out var cachedSound) && cachedSound != null)
                {
                    yield return new PlaybackObject(cachedSound, first.Volume, first.Balance);
                }

                _hitQueue.TryDequeue(out first);
            }

            _firstNode = first;
        }
    }

    private async void OsuModeManager_OnStatusChanged(OsuListenerManager.OsuStatus pre, OsuListenerManager.OsuStatus cur)
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

            var osuDir = new OsuDirectory(folder);
            using (DebugUtils.CreateTimer("InitFolder", Logger))
            {
                await osuDir.InitializeAsync(Beatmap.Filename);
            }

            if (osuDir.OsuFiles.Count <= 0)
            {
                Logger.LogWarning(
                    $"There is no available beatmaps after scanning. Directory: {folder}; File: {Beatmap.Filename}");
                return;
            }

            using (DebugUtils.CreateTimer("InitSound", Logger))
            {
                var hitsoundList = await osuDir.GetHitsoundNodesAsync(osuDir.OsuFiles[0]);
                HitsoundList = hitsoundList
                    .OrderBy(k => k.Offset)
                    .Where(k => k is PlayableNode { PlayablePriority: PlayablePriority.Primary })
                    .Cast<PlayableNode>()
                    .ToList();
            }

            RequeueNodes();
            IsStarted = true;
        }
        catch (Exception ex)
        {
            IsStarted = false;
            Logger.LogError(ex, "Error while starting a beatmap");
        }
    }

    private void OsuModeManager_OnBeatmapChanged(Beatmap? beatmap)
    {
    }

    private void OsuModeManager_OnPlayTimeChanged(int oldMs, int newMs)
    {
        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            PlayTimeList.Add(newMs);
        });

        if (IsStarted && oldMs > newMs) // Retry
        {
            RequeueNodes();
            return;
        }

        if (IsStarted && newMs > _nextReadTime)
        {
            AddHitsoundCacheInBackground(_nextReadTime, _nextReadTime + 13000);
            _nextReadTime += 10000;
        }

        if (IsStarted && SharedViewModel.Instance.LatencyTestMode)
        {
            foreach (var playbackObject in GetPlaybackHitsounds())
            {
                SharedViewModel.Instance.AudioPlaybackEngine?.PlaySound(playbackObject.CachedSound,
                    playbackObject.Volume, playbackObject.Balance);
            }
        }
    }

    private void CleanHitsoundCaches()
    {
        CachedSoundFactory.ClearCacheSounds();
        _playNodeToCachedSoundMapping.Clear();
    }

    private void RequeueNodes()
    {
        _hitQueue = new Queue<PlayableNode>(HitsoundList!);
        Application.Current.Dispatcher.InvokeAsync(() => PlayTimeList.Clear());
        _firstNode = _hitQueue.Dequeue();
        AddHitsoundCacheInBackground(0, 13000);
        _nextReadTime = 10000;
    }

    private void AddHitsoundCacheInBackground(int startTime, int endTime)
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

        if (HitsoundList == null)
        {
            Logger.LogWarning($"{nameof(HitsoundList)} is null, stop adding cache.");
            return;
        }

        var hitsoundList = HitsoundList;
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
                        Logger.LogWarning("Cached sound: " + path);
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