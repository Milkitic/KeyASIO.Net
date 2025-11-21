using Coosu.Beatmap.Extensions.Playback;
using KeyAsio.Audio;
using KeyAsio.Shared.Models;
using KeyAsio.Shared.Realtime.Services;
using KeyAsio.Shared.Utils;
using Microsoft.Extensions.Logging;

namespace KeyAsio.Shared.Realtime.AudioProviders;

public class ManiaAudioProvider : IAudioProvider
{
    private readonly ILogger<ManiaAudioProvider> _logger;
    private readonly AppSettings _appSettings;
    private readonly RealtimeProperties _realtimeProperties;
    private readonly AudioEngine _audioEngine;
    private readonly AudioCacheService _audioCacheService;
    private readonly PlaySessionManager _playSessionManager;

    private List<Queue<PlayableNode>> _hitQueue = new();
    private PlayableNode?[] _hitQueueCache = Array.Empty<PlayableNode>();

    private Queue<HitsoundNode> _playQueue = new();
    private Queue<HitsoundNode> _autoPlayQueue = new();

    private HitsoundNode? _firstAutoNode;
    private HitsoundNode? _firstPlayNode;

    public ManiaAudioProvider(ILogger<ManiaAudioProvider> logger,
        AppSettings appSettings,
        RealtimeProperties realtimeProperties,
        AudioEngine audioEngine,
        AudioCacheService audioCacheService,
        PlaySessionManager playSessionManager)
    {
        _logger = logger;
        _appSettings = appSettings;
        _realtimeProperties = realtimeProperties;
        _audioEngine = audioEngine;
        _audioCacheService = audioCacheService;
        _playSessionManager = playSessionManager;
    }

    public void FillPlaybackAudio(List<PlaybackInfo> buffer, bool includeKey)
    {
        var playTime = _realtimeProperties.PlayTime;
        var isStarted = _realtimeProperties.IsStarted;

        if (_audioEngine.CurrentDevice == null)
        {
            _logger.LogWarning("Engine not ready, return empty.");
            return;
        }

        if (!isStarted)
        {
            _logger.LogWarning("Game hasn't started, return empty.");
            return;
        }

        var first = includeKey ? _firstPlayNode : _firstAutoNode;
        if (first == null)
        {
            return;
            _logger.LogWarning("First is null, no item returned.");
        }

        if (playTime < first.Offset)
        {
            return;
            _logger.LogWarning("Haven't reached first, no item returned.");
        }

        FillNextPlaybackAudio(buffer, first, playTime, includeKey);
    }

    public void FillKeyAudio(List<PlaybackInfo> buffer, int keyIndex, int keyTotal)
    {
        using var _ = DebugUtils.CreateTimer($"GetSoundOnClick", _logger);
        var playTime = _realtimeProperties.PlayTime;
        var isStarted = _realtimeProperties.IsStarted;

        if (_audioEngine.CurrentDevice == null)
        {
            _logger.LogWarning("Engine not ready, return empty.");
            return;
        }

        if (!isStarted)
        {
            _logger.LogWarning("Game hasn't started, return empty.");
            return;
        }

        if (_hitQueue.Count - 1 < keyIndex || _hitQueueCache.Length - 1 < keyIndex)
        {
            _logger.LogWarning(
                "Key index was out of range ({KeyIndex}). Please check your key configuration to match mania columns.",
                keyIndex);
            return;
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
                    _logger.LogInformation("Dequeued and will use Col." + keyIndex);
                    break;
                }

                queue.Dequeue();
                _logger.LogInformation("Dropped Col." + keyIndex);
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
            _logger.LogDebug("Use first");
        }
        else
        {
            _logger.LogDebug("Use cache");
        }

        if (playableNode == null || !_audioCacheService.TryGetAudioByNode(playableNode, out var cachedAudio))
        {
            _logger.LogWarning("No audio returned.");
        }
        else
        {
            buffer.Add(new PlaybackInfo(cachedAudio, playableNode));
        }
    }

    public void FillAudioList(IReadOnlyList<HitsoundNode> nodeList, List<PlayableNode> keyList,
        List<HitsoundNode> playbackList)
    {
        foreach (var hitsoundNode in nodeList)
        {
            if (hitsoundNode is not PlayableNode playableNode) continue;

            if (playableNode.PlayablePriority is PlayablePriority.Sampling)
            {
                if (!_appSettings.RealtimeOptions.IgnoreStoryboardSamples)
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
        _hitQueue = GetHitQueue(_playSessionManager.KeyList, playTime);
        _hitQueueCache = new PlayableNode[_hitQueue.Count];

        _autoPlayQueue = new Queue<HitsoundNode>(_playSessionManager.KeyList);
        _playQueue = new Queue<HitsoundNode>(_playSessionManager.PlaybackList.Where(k => k.Offset >= playTime));
        _autoPlayQueue.TryDequeue(out _firstAutoNode);
        _playQueue.TryDequeue(out _firstPlayNode);
    }

    private List<Queue<PlayableNode>> GetHitQueue(IReadOnlyList<PlayableNode> keyList, int playTime)
    {
        if (_playSessionManager.OsuFile == null)
            return new List<Queue<PlayableNode>>();

        var keyCount = (int)_playSessionManager.OsuFile.Difficulty.CircleSize;
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

    private void FillNextPlaybackAudio(List<PlaybackInfo> buffer, HitsoundNode? firstNode, int playTime, bool includeKey)
    {
        while (firstNode != null)
        {
            if (playTime < firstNode.Offset)
            {
                break;
            }

            if (playTime < firstNode.Offset + 200 &&
                _audioCacheService.TryGetAudioByNode(firstNode, out var cachedSound))
            {
                buffer.Add(new PlaybackInfo(cachedSound, firstNode));
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
}