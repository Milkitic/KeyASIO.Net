using Coosu.Beatmap.Extensions.Playback;
using KeyAsio.Audio;
using KeyAsio.Shared.Models;
using KeyAsio.Shared.Realtime.Services;
using KeyAsio.Shared.Utils;
using Microsoft.Extensions.Logging;

namespace KeyAsio.Shared.Realtime.AudioProviders;

public class StandardHitsoundSequencer : IHitsoundSequencer
{
    private readonly ILogger<StandardHitsoundSequencer> _logger;
    private readonly AppSettings _appSettings;
    private readonly RealtimeSessionContext _realtimeSessionContext;
    private readonly AudioEngine _audioEngine;
    private readonly AudioCacheService _audioCacheService;
    private readonly GameplaySessionManager _gameplaySessionManager;

    private Queue<PlayableNode> _hitQueue = new();
    private Queue<HitsoundNode> _playQueue = new();

    private PlayableNode? _firstNode;
    private HitsoundNode? _firstPlayNode;

    public StandardHitsoundSequencer(ILogger<StandardHitsoundSequencer> logger,
        AppSettings appSettings,
        RealtimeSessionContext realtimeSessionContext,
        AudioEngine audioEngine,
        AudioCacheService audioCacheService,
        GameplaySessionManager gameplaySessionManager)
    {
        _logger = logger;
        _appSettings = appSettings;
        _realtimeSessionContext = realtimeSessionContext;
        _audioEngine = audioEngine;
        _audioCacheService = audioCacheService;
        _gameplaySessionManager = gameplaySessionManager;
    }

    public int KeyThresholdMilliseconds { get; set; } = 100;

    public void FillPlaybackAudio(List<PlaybackInfo> buffer, bool includeKey)
    {
        var playTime = _realtimeSessionContext.PlayTime;
        var isStarted = _realtimeSessionContext.IsStarted;

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

        var first = includeKey ? _firstPlayNode : _firstNode;
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

        var playTime = _realtimeSessionContext.PlayTime;
        var isStarted = _realtimeSessionContext.IsStarted;

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

        var first = _firstNode;
        if (first == null)
        {
            _logger.LogWarning("First is null, no item returned.");
            return;
        }

        _logger.LogDebug($"Click: {playTime}; First node: {first.Offset}");

        if (playTime < first.Offset - KeyThresholdMilliseconds)
        {
            _logger.LogWarning("Haven't reached first, no item returned.");
            return;
        }

        if (playTime < first.Offset + KeyThresholdMilliseconds) // click soon~0~late
        {
            FillNextKeyAudio(buffer, first, playTime, false);
        }

        FillNextKeyAudio(buffer, first, playTime, true);
    }

    public void FillAudioList(IReadOnlyList<HitsoundNode> nodeList, List<PlayableNode> keyList,
        List<HitsoundNode> playbackList)
    {
        var secondaryCache = new List<PlayableNode>();
        foreach (var hitsoundNode in nodeList)
        {
            if (hitsoundNode is not PlayableNode playableNode)
            {
                var controlNode = (ControlNode)hitsoundNode;
                if (controlNode.ControlType is ControlType.ChangeBalance or ControlType.None) continue;
                if (_appSettings.RealtimeOptions.IgnoreSliderTicksAndSlides) continue;
                playbackList.Add(controlNode);
                continue;
            }

            if (playableNode.PlayablePriority == PlayablePriority.Primary)
            {
                CheckSecondary();
                secondaryCache.Clear();
                keyList.Add(playableNode);
            }
            else if (playableNode.PlayablePriority is PlayablePriority.Secondary)
            {
                var sliderTailBehavior = _appSettings.RealtimeOptions.SliderTailPlaybackBehavior;
                if (sliderTailBehavior == SliderTailPlaybackBehavior.Normal)
                {
                    playbackList.Add(playableNode);
                }
                else if (sliderTailBehavior == SliderTailPlaybackBehavior.KeepReverse)
                {
                    secondaryCache.Add(playableNode);
                }
            }
            else if (playableNode.PlayablePriority is PlayablePriority.Effects)
            {
                if (!_appSettings.RealtimeOptions.IgnoreSliderTicksAndSlides)
                {
                    playbackList.Add(playableNode);
                }
            }
            else if (playableNode.PlayablePriority is PlayablePriority.Sampling)
            {
                if (!_appSettings.RealtimeOptions.IgnoreStoryboardSamples)
                {
                    playbackList.Add(playableNode);
                }
            }
        }

        CheckSecondary();

        void CheckSecondary()
        {
            if (secondaryCache.Count <= 1) return;
            playbackList.AddRange(secondaryCache);
        }
    }

    public void ResetNodes(int playTime)
    {
        _hitQueue = new Queue<PlayableNode>(_gameplaySessionManager.KeyList);
        _playQueue = new Queue<HitsoundNode>(_gameplaySessionManager.PlaybackList
            .Where(k => k.Offset >= _realtimeSessionContext.PlayTime));
        _hitQueue.TryDequeue(out _firstNode);
        _playQueue.TryDequeue(out _firstPlayNode);
    }

    private void FillNextKeyAudio(List<PlaybackInfo> buffer, PlayableNode? firstNode, int playTime, bool checkPreTiming)
    {
        int counter = 0;
        bool isFirst = true;
        PlayableNode? preNode = null;
        while (firstNode != null)
        {
            if (preNode?.Guid != firstNode.Guid)
            {
                if (!isFirst && !checkPreTiming && playTime < firstNode.Offset - 3)
                {
                    //_logger.LogWarning($"Haven't reached first, return empty.");
                    break;
                }

                if (checkPreTiming && playTime >= firstNode.Offset + KeyThresholdMilliseconds)
                {
                    _hitQueue.TryDequeue(out firstNode);
                    continue;
                }
            }

            isFirst = false;
            checkPreTiming = false;
            if (_audioCacheService.TryGetAudioByNode(firstNode, out var cachedSound))
            {
                counter++;
                preNode = firstNode;
                buffer.Add(new PlaybackInfo(cachedSound, firstNode));
            }

            _hitQueue.TryDequeue(out firstNode);
        }

        _firstNode = firstNode;
        if (counter == 0)
        {
            _logger.LogWarning($"Counter is zero, no item returned.");
        }
    }

    private void FillNextPlaybackAudio(List<PlaybackInfo> buffer, HitsoundNode? firstNode, int playTime,
        bool includeKey)
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
                _hitQueue.TryDequeue(out var node);
                firstNode = node;
            }
        }

        if (includeKey)
        {
            _firstPlayNode = firstNode;
        }
        else
        {
            _firstNode = (PlayableNode?)firstNode;
        }
    }
}