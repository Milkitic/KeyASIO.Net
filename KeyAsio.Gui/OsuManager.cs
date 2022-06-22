using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Coosu.Beatmap;
using Coosu.Beatmap.Extensions.Playback;
using KeyAsio.Gui.Configuration;
using KeyAsio.Gui.Utils;
using Microsoft.Extensions.Logging;
using Milki.Extensions.MixPlayer.NAudioExtensions.Wave;
using OsuRTDataProvider.BeatmapInfo;
using OsuRTDataProvider.Listen;

namespace KeyAsio.Gui;

public class OsuManager : ViewModelBase
{
    public static OsuManager Instance { get; } = new();
    private static readonly ILogger Logger = SharedUtils.GetLogger(nameof(ConfigurationFactory));

    private OsuListenerManager.OsuStatus _osuStatus;
    private int _playTime;
    private Beatmap? _beatmap;
    private bool _isStarted;
    private object _isStartedLock = new();
    private List<PlayableNode>? _hitsoundList;

    private Queue<PlayableNode> _hitQueue = new();
    private PlayableNode? _firstNode;
    private string? _folder;
    private int _nextReadTime;

    public int PlayTime
    {
        get => _playTime;
        set
        {
            if (value == _playTime) return;
            var val = _playTime;
            _playTime = value;
            OsuModeManager_OnPlayTimeChanged(val, value);
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

    public async ValueTask<IReadOnlyCollection<CachedSound>> GetCurrentHitsounds()
    {
        const int thresholdMs = 50;
        var audioPlaybackEngine = SharedViewModel.Instance.AudioPlaybackEngine;
        if (audioPlaybackEngine == null) return Array.Empty<CachedSound>();

        if (!IsStarted)
        {
            return Array.Empty<CachedSound>();
        }

        var first = _firstNode;
        if (first == null)
        {
            return Array.Empty<CachedSound>();
        }

        var playTime = PlayTime;
        if (playTime < first.Offset - thresholdMs)
        {
            return Array.Empty<CachedSound>();
        }

        if (playTime < first.Offset + thresholdMs)
        {
            return await GetHitsoundList(false);
        }

        return await GetHitsoundList(true);

        async ValueTask<IReadOnlyCollection<CachedSound>> GetHitsoundList(bool checkPreTiming)
        {
            var list = new List<CachedSound>();
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
                var identifier = first.UseUserSkin ? "internal" : null;
                var waveFormat = audioPlaybackEngine.WaveFormat;
                if (first.Filename != null)
                {
                    var path = Path.Combine(_folder!, first.Filename);
                    var cachedSound = await CachedSoundFactory.GetOrCreateCacheSound(waveFormat, path, identifier);
                    if (cachedSound != null)
                    {
                        list.Add(cachedSound);
                    }
                }

                _hitQueue.TryDequeue(out first);
            }

            _firstNode = first;
            return list;
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
        IsStarted = false;
        _playTime = 0;
        PlayTime = 0;
    }

    private async Task StartAsync()
    {
        // Load beatmap & hitsounds
        try
        {
            if (Beatmap == null)
            {
                throw new Exception("The beatmap is null!");
            }

            var folder = Path.GetDirectoryName(Beatmap.FilenameFull);
            if (_folder != folder)
            {
                CleanHitsoundCaches();
            }

            _folder = folder;
            if (folder == null)
            {
                throw new Exception("The beatmap folder is null!");
            }

            var osuDir = new OsuDirectory(folder);
            await osuDir.InitializeAsync(Beatmap.Filename);
            if (osuDir.OsuFiles.Count <= 0)
            {
                Logger.LogWarning(
                    $"There is no available beatmaps after scanning. Directory: {folder}; File: {Beatmap.Filename}");
                return;
            }

            var hitsoundList = await osuDir.GetHitsoundNodesAsync(osuDir.OsuFiles[0]);
            HitsoundList = hitsoundList
                .Where(k => k is PlayableNode { PlayablePriority: PlayablePriority.Primary })
                .Cast<PlayableNode>()
                .ToList();
            RequeueNodes();
            IsStarted = true;
        }
        catch (Exception ex)
        {
            IsStarted = false;
            Logger.LogError(ex, "Error while starting a beatmap");
        }
    }

    private void OsuModeManager_OnBeatmapChanged(Beatmap? map)
    {
    }

    private void OsuModeManager_OnPlayTimeChanged(int oldMs, int newMs)
    {
        if (oldMs < newMs && IsStarted) // Retry
        {
            RequeueNodes();
            return;
        }

        if (newMs > _nextReadTime)
        {
            AddHitsoundCacheInBackground(_nextReadTime, _nextReadTime + 13000);
            _nextReadTime += 10000;
        }
    }

    private static void CleanHitsoundCaches()
    {
        CachedSoundFactory.ClearCacheSounds();
    }

    private void RequeueNodes()
    {
        _hitQueue = new Queue<PlayableNode>(HitsoundList!);
        _firstNode = _hitQueue.Dequeue();
        AddHitsoundCacheInBackground(0, 13000);
        _nextReadTime = 10000;
    }

    private void AddHitsoundCacheInBackground(int startTime, int endTime)
    {
        if (_folder == null) return;
        if (SharedViewModel.Instance.AudioPlaybackEngine == null) return;
        if (HitsoundList == null) return;
        var hitsoundList = HitsoundList;
        var folder = _folder;
        var waveFormat = SharedViewModel.Instance.AudioPlaybackEngine.WaveFormat;
        Task.Run(() =>
        {
            hitsoundList
                .Where(k => k.Offset >= startTime && k.Offset < endTime)
                .AsParallel()
                .WithDegreeOfParallelism(Environment.ProcessorCount == 1 ? 1 : Environment.ProcessorCount / 2)
                .ForAll(playableNode =>
                {
                    if (!IsStarted) return;
                    if (playableNode.Filename == null) return;

                    var path = Path.Combine(folder, playableNode.Filename);
                    var identifier = playableNode.UseUserSkin ? "internal" : null;
                    CachedSoundFactory.GetOrCreateCacheSound(waveFormat, path, identifier).Wait();
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