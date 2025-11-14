using Coosu.Beatmap.Extensions.Playback;
using KeyAsio.MemoryReading.Logging;
using KeyAsio.Shared.Audio;
using KeyAsio.Shared.Models;
using KeyAsio.Shared.Utils;
using Milki.Extensions.Configuration;

namespace KeyAsio.Shared.Realtime.AudioProviders;

public class ManiaAudioProvider : IAudioProvider
{
    private static readonly ILogger Logger = LogUtils.GetLogger(nameof(ManiaAudioProvider));
    private readonly RealtimeModeManager _realtimeModeManager;
    private readonly SharedViewModel _sharedViewModel;
    private List<Queue<PlayableNode>> _hitQueue = new();
    private PlayableNode?[] _hitQueueCache = Array.Empty<PlayableNode>();

    private Queue<HitsoundNode> _playQueue = new();
    private Queue<HitsoundNode> _autoPlayQueue = new();

    private HitsoundNode? _firstAutoNode;
    private HitsoundNode? _firstPlayNode;

    public ManiaAudioProvider(RealtimeModeManager realtimeModeManager, SharedViewModel sharedViewModel)
    {
        _realtimeModeManager = realtimeModeManager;
        _sharedViewModel = sharedViewModel;
    }

    public bool IsStarted => _realtimeModeManager.IsStarted;
    public int PlayTime => _realtimeModeManager.PlayTime;
    public AudioEngine? AudioEngine => _sharedViewModel.AudioEngine;
    public AppSettings AppSettings => ConfigurationFactory.GetConfiguration<AppSettings>();

    public IEnumerable<PlaybackInfo> GetPlaybackAudio(bool includeKey)
    {
        var playTime = PlayTime;
        var audioEngine = AudioEngine;
        var isStarted = IsStarted;

        if (audioEngine == null) return ReturnDefaultAndLog("Engine not ready, return empty.", LogLevel.Warning);
        if (!isStarted) return ReturnDefaultAndLog("Game hasn't started, return empty.", LogLevel.Warning);

        var first = includeKey ? _firstPlayNode : _firstAutoNode;
        if (first == null)
        {
            return Array.Empty<PlaybackInfo>();
            return ReturnDefaultAndLog("First is null, no item returned.", LogLevel.Warning);
        }

        if (playTime < first.Offset)
        {
            return Array.Empty<PlaybackInfo>();
            return ReturnDefaultAndLog("Haven't reached first, no item returned.", LogLevel.Warning);
        }

        return GetNextPlaybackAudio(first, playTime, includeKey);
    }

    public IEnumerable<PlaybackInfo> GetKeyAudio(int keyIndex, int keyTotal)
    {
        using var _ = DebugUtils.CreateTimer($"GetSoundOnClick", Logger);
        var playTime = PlayTime;
        var audioEngine = AudioEngine;
        var isStarted = IsStarted;

        if (audioEngine == null) return ReturnDefaultAndLog("Engine not ready, return empty.", LogLevel.Warning);
        if (!isStarted) return ReturnDefaultAndLog("Game hasn't started, return empty.", LogLevel.Warning);
        if (_hitQueue.Count - 1 < keyIndex || _hitQueueCache.Length - 1 < keyIndex)
        {
            Logger.Warn($"Key index was out of range ({keyIndex}). Please check your key configuration to match mania columns.");
            return Enumerable.Empty<PlaybackInfo>();
        }

        var queue = _hitQueue[keyIndex];
        while (true)
        {
            if (queue.TryPeek(out var node))
            {
                if (playTime < node.Offset - 80 /*odMax*/)
                {
                    _hitQueueCache[keyIndex] = null;
                    break;
                }

                if (playTime <= node.Offset + 50 /*odMax*/)
                {
                    _hitQueueCache[keyIndex] = queue.Dequeue();
                    Logger.Info("Dequeued and will use Col." + keyIndex);
                    break;
                }

                queue.Dequeue();
                Logger.Info("Dropped Col." + keyIndex);
                _hitQueueCache[keyIndex] = null;
            }
            else
            {
                _hitQueueCache[keyIndex] = null;
                break;
            }
        }

        var playableNode = _hitQueueCache[keyIndex];
        if (playableNode == null)
        {
            _hitQueue[keyIndex].TryPeek(out playableNode);
            Logger.Debug("Use first");
        }
        else
        {
            Logger.Debug("Use cache");
        }

        if (playableNode != null && _realtimeModeManager.TryGetAudioByNode(playableNode, out var cachedSound))
        {
            return new[] { new PlaybackInfo(cachedSound, playableNode) };
        }

        return ReturnDefaultAndLog("No audio returned.", LogLevel.Warning);
    }

    public void FillAudioList(IReadOnlyList<HitsoundNode> nodeList, List<PlayableNode> keyList, List<HitsoundNode> playbackList)
    {
        foreach (var hitsoundNode in nodeList)
        {
            if (hitsoundNode is not PlayableNode playableNode) continue;

            if (playableNode.PlayablePriority is PlayablePriority.Sampling)
            {
                if (!AppSettings.RealtimeOptions.IgnoreStoryboardSamples)
                {
                    playbackList.Add(playableNode);
                }
            }
            else
            {
                keyList.Add(playableNode);
            }
        }
    }

    public void ResetNodes(int playTime)
    {
        _hitQueue = GetHitQueue(_realtimeModeManager.KeyList, playTime);
        _hitQueueCache = new PlayableNode[_hitQueue.Count];

        _autoPlayQueue = new Queue<HitsoundNode>(_realtimeModeManager.KeyList);
        _playQueue = new Queue<HitsoundNode>(_realtimeModeManager.PlaybackList.Where(k => k.Offset >= playTime));
        _autoPlayQueue.TryDequeue(out _firstAutoNode);
        _playQueue.TryDequeue(out _firstPlayNode);
    }

    private List<Queue<PlayableNode>> GetHitQueue(IReadOnlyList<PlayableNode> keyList, int playTime)
    {
        if (_realtimeModeManager.OsuFile == null)
            return new List<Queue<PlayableNode>>();

        var keyCount = (int)_realtimeModeManager.OsuFile.Difficulty.CircleSize;
        var list = new List<Queue<PlayableNode>>(keyCount);
        for (int i = 0; i < keyCount; i++)
        {
            list.Add(new Queue<PlayableNode>());
        }

        foreach (var playableNode in keyList.Where(k => k.Offset >= playTime))
        {
            var ratio = (playableNode.Balance + 1d) / 2;
            var column = (int)Math.Round(ratio * keyCount - 0.5);
            list[column].Enqueue(playableNode);
        }

        return list;
    }

    private IEnumerable<PlaybackInfo> GetNextPlaybackAudio(HitsoundNode? firstNode, int playTime, bool includeKey)
    {
        while (firstNode != null)
        {
            if (playTime < firstNode.Offset)
            {
                break;
            }

            if (playTime < firstNode.Offset + 200 &&
                _realtimeModeManager.TryGetAudioByNode(firstNode, out var cachedSound))
            {
                yield return new PlaybackInfo(cachedSound, firstNode);
            }

            if (includeKey)
            {
                _playQueue.TryDequeue(out firstNode);
            }
            else
            {
                _autoPlayQueue.TryDequeue(out firstNode);
            }
        }

        if (includeKey)
        {
            _firstPlayNode = firstNode;
        }
        else
        {
            _firstAutoNode = firstNode;
        }
    }

    private static IEnumerable<PlaybackInfo> ReturnDefaultAndLog(string message, LogLevel logLevel = LogLevel.Debug)
    {
        Logger.Log(logLevel, message);
        return Array.Empty<PlaybackInfo>();
    }
}
