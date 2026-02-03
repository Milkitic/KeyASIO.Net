using KeyAsio.Core.Audio;
using KeyAsio.Shared.Hitsounds.Playback;
using KeyAsio.Shared.Models;
using KeyAsio.Shared.Sync.Services;
using KeyAsio.Shared.Utils;
using Microsoft.Extensions.Logging;

namespace KeyAsio.Shared.Sync.AudioProviders;

public class ManiaHitsoundSequencer : IHitsoundSequencer
{
    private readonly ILogger<ManiaHitsoundSequencer> _logger;
    private readonly AppSettings _appSettings;
    private readonly SyncSessionContext _syncSessionContext;
    private readonly IPlaybackEngine _playbackEngine;
    private readonly GameplayAudioService _gameplayAudioService;
    private readonly GameplaySessionManager _gameplaySessionManager;

    private List<Queue<SampleEvent>> _hitQueue = new();
    private SampleEvent?[] _hitQueueCache = Array.Empty<SampleEvent>();

    private Queue<PlaybackEvent> _playQueue = new();
    private Queue<PlaybackEvent> _autoPlayQueue = new();

    private PlaybackEvent? _firstAutoNode;
    private PlaybackEvent? _firstPlayNode;

    public ManiaHitsoundSequencer(ILogger<ManiaHitsoundSequencer> logger,
        AppSettings appSettings,
        SyncSessionContext syncSessionContext,
        IPlaybackEngine playbackEngine,
        GameplayAudioService gameplayAudioService,
        GameplaySessionManager gameplaySessionManager)
    {
        _logger = logger;
        _appSettings = appSettings;
        _syncSessionContext = syncSessionContext;
        _playbackEngine = playbackEngine;
        _gameplayAudioService = gameplayAudioService;
        _gameplaySessionManager = gameplaySessionManager;
    }

    public void ProcessAutoPlay(List<PlaybackInfo> buffer, bool processHitQueueAsAuto)
    {
        var playTime = _syncSessionContext.PlayTime;
        var isStarted = _syncSessionContext.IsStarted;

        if (_playbackEngine.CurrentDevice == null)
        {
            _logger.LogWarning("Engine not ready, return empty.");
            return;
        }

        if (!isStarted)
        {
            _logger.LogWarning("Game hasn't started, return empty.");
            return;
        }

        var first = processHitQueueAsAuto ? _firstPlayNode : _firstAutoNode;
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

        FillNextPlaybackAudio(buffer, first, playTime, processHitQueueAsAuto);
    }

    public void ProcessInteraction(List<PlaybackInfo> buffer, int keyIndex, int keyTotal)
    {
        using var _ = DebugUtils.CreateTimer($"GetSoundOnClick", _logger);
        var playTime = _syncSessionContext.PlayTime;
        var isStarted = _syncSessionContext.IsStarted;

        if (_playbackEngine.CurrentDevice == null)
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

        if (playableNode == null || !_gameplayAudioService.TryGetAudioByNode(playableNode, out var cachedAudio))
        {
            _logger.LogWarning("No audio returned.");
        }
        else
        {
            buffer.Add(new PlaybackInfo(cachedAudio, playableNode));
        }
    }

    public void FillAudioList(IReadOnlyList<PlaybackEvent> nodeList, List<SampleEvent> keyList,
        List<PlaybackEvent> playbackList)
    {
        foreach (var hitsoundNode in nodeList)
        {
            if (hitsoundNode is not SampleEvent playableNode) continue;

            if (playableNode.Layer is SampleLayer.Sampling)
            {
                if (!_appSettings.Sync.Filters.DisableStoryboardSamples)
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

    public void SeekTo(int playTime)
    {
        _hitQueue = GetHitQueue(_gameplaySessionManager.KeyList, playTime);
        _hitQueueCache = new SampleEvent[_hitQueue.Count];

        _autoPlayQueue = new Queue<PlaybackEvent>(_gameplaySessionManager.KeyList);
        _playQueue = new Queue<PlaybackEvent>(_gameplaySessionManager.PlaybackList.Where(k => k.Offset >= playTime));
        _autoPlayQueue.TryDequeue(out _firstAutoNode);
        _playQueue.TryDequeue(out _firstPlayNode);
    }

    private List<Queue<SampleEvent>> GetHitQueue(IReadOnlyList<SampleEvent> keyList, int playTime)
    {
        if (_gameplaySessionManager.OsuFile == null)
            return new List<Queue<SampleEvent>>();

        var keyCount = (int)_gameplaySessionManager.OsuFile.Difficulty.CircleSize;
        var list = new List<Queue<SampleEvent>>(keyCount);
        for (int i = 0; i < keyCount; i++)
        {
            list.Add(new Queue<SampleEvent>());
        }

        foreach (var playableNode in keyList.Where(k => k.Offset >= playTime))
        {
            var ratio = (playableNode.Balance + 1d) / 2;
            var column = (int)Math.Round(ratio * keyCount - 0.5);
            list[column].Enqueue(playableNode);
        }

        return list;
    }

    private void FillNextPlaybackAudio(List<PlaybackInfo> buffer, PlaybackEvent? firstNode, int playTime,
        bool includeKey)
    {
        while (firstNode != null)
        {
            if (playTime < firstNode.Offset)
            {
                break;
            }

            if (playTime < firstNode.Offset + 200 &&
                _gameplayAudioService.TryGetAudioByNode(firstNode, out var cachedSound))
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