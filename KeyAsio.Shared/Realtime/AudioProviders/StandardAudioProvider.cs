using Coosu.Beatmap.Extensions.Playback;
using KeyAsio.Audio;
using KeyAsio.Shared.Models;
using KeyAsio.Shared.Realtime.Services;
using KeyAsio.Shared.Utils;
using Microsoft.Extensions.Logging;
using Milki.Extensions.Configuration;

namespace KeyAsio.Shared.Realtime.AudioProviders;

public class StandardAudioProvider : IAudioProvider
{
    private readonly ILogger<StandardAudioProvider> _logger;
    private readonly AudioEngine _audioEngine;
    private readonly AudioCacheService _audioCacheService;
    private readonly RealtimeModeManager _realtimeModeManager;

    private Queue<PlayableNode> _hitQueue = new();
    private Queue<HitsoundNode> _playQueue = new();

    private PlayableNode? _firstNode;
    private HitsoundNode? _firstPlayNode;

    public StandardAudioProvider(ILogger<StandardAudioProvider> logger, AudioEngine audioEngine,
        AudioCacheService audioCacheService, RealtimeModeManager realtimeModeManager)
    {
        _logger = logger;
        _audioEngine = audioEngine;
        _audioCacheService = audioCacheService;
        _realtimeModeManager = realtimeModeManager;
    }

    public int KeyThresholdMilliseconds { get; set; } = 100;
    public bool IsStarted => _realtimeModeManager.IsStarted;
    public int PlayTime => _realtimeModeManager.PlayTime;
    public AppSettings AppSettings => ConfigurationFactory.GetConfiguration<AppSettings>();

    public IEnumerable<PlaybackInfo> GetPlaybackAudio(bool includeKey)
    {
        var playTime = PlayTime;
        var isStarted = IsStarted;

        if (_audioEngine.CurrentDevice == null)
        {
            _logger.LogWarning("Engine not ready, return empty.");
            return [];
        }

        if (!isStarted)
        {
            _logger.LogWarning("Game hasn't started, return empty.");
            return [];
        }

        var first = includeKey ? _firstPlayNode : _firstNode;
        if (first == null)
        {
            return [];
            _logger.LogWarning("First is null, no item returned.");
        }

        if (playTime < first.Offset)
        {
            return [];
            _logger.LogWarning("Haven't reached first, no item returned.");
        }

        return GetNextPlaybackAudio(first, playTime, includeKey);
    }

    public IEnumerable<PlaybackInfo> GetKeyAudio(int keyIndex, int keyTotal)
    {
        using var _ = DebugUtils.CreateTimer($"GetSoundOnClick", _logger);

        var playTime = PlayTime;
        var isStarted = IsStarted;

        if (_audioEngine.CurrentDevice == null)
        {
            _logger.LogWarning("Engine not ready, return empty.");
            return [];
        }

        if (!isStarted)
        {
            _logger.LogWarning("Game hasn't started, return empty.");
            return [];
        }

        var first = _firstNode;
        if (first == null)
        {
            _logger.LogWarning("First is null, no item returned.");
            return [];
        }

        _logger.LogDebug($"Click: {playTime}; First node: {first.Offset}");

        if (playTime < first.Offset - KeyThresholdMilliseconds)
        {
            _logger.LogWarning("Haven't reached first, no item returned.");
            return [];
        }

        if (playTime < first.Offset + KeyThresholdMilliseconds) // click soon~0~late
        {
            return GetNextKeyAudio(first, playTime, false);
        }

        return GetNextKeyAudio(first, playTime, true);
    }

    public void FillAudioList(IReadOnlyList<HitsoundNode> nodeList, List<PlayableNode> keyList, List<HitsoundNode> playbackList)
    {
        var secondaryCache = new List<PlayableNode>();
        foreach (var hitsoundNode in nodeList)
        {
            if (hitsoundNode is not PlayableNode playableNode)
            {
                var controlNode = (ControlNode)hitsoundNode;
                if (controlNode.ControlType is ControlType.ChangeBalance or ControlType.None) continue;
                if (AppSettings.RealtimeOptions.IgnoreSliderTicksAndSlides) continue;
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
                var sliderTailBehavior = AppSettings.RealtimeOptions.SliderTailPlaybackBehavior;
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
                if (!AppSettings.RealtimeOptions.IgnoreSliderTicksAndSlides)
                {
                    playbackList.Add(playableNode);
                }
            }
            else if (playableNode.PlayablePriority is PlayablePriority.Sampling)
            {
                if (!AppSettings.RealtimeOptions.IgnoreStoryboardSamples)
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
        _hitQueue = new Queue<PlayableNode>(_realtimeModeManager.KeyList);
        _playQueue = new Queue<HitsoundNode>(_realtimeModeManager.PlaybackList.Where(k => k.Offset >= PlayTime));
        _hitQueue.TryDequeue(out _firstNode);
        _playQueue.TryDequeue(out _firstPlayNode);
    }

    private IEnumerable<PlaybackInfo> GetNextKeyAudio(PlayableNode? firstNode, int playTime, bool checkPreTiming)
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
                    //Logger.LogWarning($"Haven't reached first, return empty.");
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
                yield return new PlaybackInfo(cachedSound, firstNode);
            }

            _hitQueue.TryDequeue(out firstNode);
        }

        _firstNode = firstNode;
        if (counter == 0)
        {
            _logger.LogWarning($"Counter is zero, no item returned.");
        }
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
                _audioCacheService.TryGetAudioByNode(firstNode, out var cachedSound))
            {
                yield return new PlaybackInfo(cachedSound, firstNode);
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