using Coosu.Beatmap.Extensions.Playback;
using KeyAsio.Audio;
using KeyAsio.Shared.Models;
using KeyAsio.Shared.Realtime.Services;
using Microsoft.Extensions.Logging;

namespace KeyAsio.Shared.Realtime.AudioProviders;

public class StandardHitsoundSequencer : IHitsoundSequencer
{
    private const int AudioLatencyTolerance = 200;

    private readonly ILogger<StandardHitsoundSequencer> _logger;
    private readonly AppSettings _appSettings;
    private readonly RealtimeSessionContext _realtimeSessionContext;
    private readonly AudioEngine _audioEngine;
    private readonly AudioCacheService _audioCacheService;
    private readonly GameplaySessionManager _gameplaySessionManager;

    private Queue<PlayableNode> _hitQueue = new();
    private Queue<HitsoundNode> _playbackQueue = new();

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

    public void SeekTo(int playTime)
    {
        _hitQueue = new Queue<PlayableNode>(_gameplaySessionManager.KeyList);
        _playbackQueue = new Queue<HitsoundNode>(_gameplaySessionManager.PlaybackList
            .Where(k => k.Offset >= _realtimeSessionContext.PlayTime));
    }

    public void ProcessAutoPlay(List<PlaybackInfo> buffer, bool processHitQueueAsAuto)
    {
        if (!IsEngineReady()) return;

        var first = processHitQueueAsAuto
            ? (_playbackQueue.TryPeek(out var pNode) ? pNode : null)
            : (_hitQueue.TryPeek(out var hNode) ? hNode : null);

        if (first == null)
        {
            return;
        }

        var playTime = _realtimeSessionContext.PlayTime;
        if (playTime < first.Offset)
        {
            return;
        }

        // 如果需要，将 HitQueue 当作自动播放处理（例如回放模式或 Auto 模式）
        if (processHitQueueAsAuto)
        {
            ProcessTimeBasedQueue(buffer, _playbackQueue, playTime);
        }
        else
        {
            ProcessTimeBasedQueue(buffer, _hitQueue, playTime);
        }
    }

    public void ProcessInteraction(List<PlaybackInfo> buffer, int keyIndex, int keyTotal)
    {
        if (!IsEngineReady()) return;

        var playTime = _realtimeSessionContext.PlayTime;

        // 用于处理同组音符（堆叠/Chord）
        bool hasHit = false;
        Guid? currentGroupGuid = null;

        // 循环处理队列，直到：
        // 1. 队列空了
        // 2. 判定太早（Early Reject）
        // 3. 判定命中（Hit）并处理完该组所有音符
        while (_hitQueue.TryPeek(out var node))
        {
            // 计算时间差
            // diff > 0: 此时刻在音符之后 (Late)
            // diff < 0: 此时刻在音符之前 (Early)
            var diff = playTime - node.Offset;

            // --- 情况 1: 已经命中过，正在处理同组音符 (Chord) ---
            if (hasHit)
            {
                // 如果 GUID 变了，说明这一组（Chord）处理完了，停止
                if (node.Guid != currentGroupGuid)
                {
                    return;
                }

                // 同组音符，直接播放，不需要再判定时间
                DequeueAndPlay(buffer, _hitQueue);
                continue;
            }

            // --- 情况 2: 音符已彻底过期 (Missed) ---
            // 例如：当前 1500ms，音符 1000ms，阈值 100ms。
            // 1500 > 1000 + 100 -> 过期。
            if (diff > KeyThresholdMilliseconds)
            {
                //_logger.LogDebug("Pruning expired node at {Offset} (Current: {PlayTime})", node.Offset, playTime);
                _hitQueue.Dequeue(); // 移除过期音符
                continue; // 【关键修复】：不返回，继续用当次点击去检查下一个音符！
            }

            // --- 情况 3: 点击太早 (Too Early) ---
            // 例如：当前 800ms，音符 1000ms，阈值 100ms。
            // 800 < 1000 - 100 -> 太早。
            if (diff < -KeyThresholdMilliseconds)
            {
                // 还没到判定窗口，且因为队列是有序的，后面的肯定也更早。
                // 停止处理，等待时间流逝。
                return;
            }

            // --- 情况 4: 命中判定窗口 (Hit) ---
            // 代码能走到这，说明：Offset - Threshold <= playTime <= Offset + Threshold

            //_logger.LogDebug("Hit node at {Offset} (Current: {PlayTime})", node.Offset, playTime);

            // 记录状态，标记为已命中
            hasHit = true;
            currentGroupGuid = node.Guid;

            // 播放并移除
            DequeueAndPlay(buffer, _hitQueue);

            // 循环继续，去检查是否还有同 GUID 的重叠音符
        }
    }

    private bool IsEngineReady()
    {
        if (_audioEngine.CurrentDevice == null)
        {
            _logger.LogWarning("Engine not ready.");
            return false;
        }

        if (!_realtimeSessionContext.IsStarted)
        {
            _logger.LogWarning("Game hasn't started.");
            return false;
        }

        return true;
    }

    public void FillAudioList(IReadOnlyList<HitsoundNode> nodeList, List<PlayableNode> keyList,
        List<HitsoundNode> playbackList)
    {
        var secondaryCache = new List<PlayableNode>();
        var options = _appSettings.RealtimeOptions;

        foreach (var hitsoundNode in nodeList)
        {
            if (hitsoundNode is not PlayableNode playableNode)
            {
                if (hitsoundNode is ControlNode controlNode &&
                    controlNode.ControlType != ControlType.ChangeBalance &&
                    controlNode.ControlType != ControlType.None &&
                    !options.IgnoreSliderTicksAndSlides)
                {
                    playbackList.Add(controlNode);
                }

                continue;
            }

            switch (playableNode.PlayablePriority)
            {
                case PlayablePriority.Primary:
                    CheckSecondary();
                    secondaryCache.Clear();
                    keyList.Add(playableNode);
                    break;
                case PlayablePriority.Secondary:
                    if (options.SliderTailPlaybackBehavior == SliderTailPlaybackBehavior.Normal)
                        playbackList.Add(playableNode);
                    else if (options.SliderTailPlaybackBehavior == SliderTailPlaybackBehavior.KeepReverse)
                        secondaryCache.Add(playableNode);
                    break;
                case PlayablePriority.Effects:
                    if (!options.IgnoreSliderTicksAndSlides)
                        playbackList.Add(playableNode);
                    break;
                case PlayablePriority.Sampling:
                    if (!options.IgnoreStoryboardSamples)
                        playbackList.Add(playableNode);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        CheckSecondary();

        void CheckSecondary()
        {
            if (secondaryCache.Count <= 1) return;
            playbackList.AddRange(secondaryCache);
        }
    }

    private void ProcessTimeBasedQueue<T>(List<PlaybackInfo> buffer, Queue<T> queue, int playTime)
        where T : HitsoundNode
    {
        while (queue.TryPeek(out var node))
        {
            if (playTime < node.Offset)
            {
                // 时间未到
                break;
            }

            // 只有在延迟容忍度内才播放
            if (playTime < node.Offset + AudioLatencyTolerance)
            {
                if (_audioCacheService.TryGetAudioByNode(node, out var cachedSound))
                {
                    buffer.Add(new PlaybackInfo(cachedSound, node));
                }
            }

            // 无论是否播放（播放了 or 超时了 or 找不到资源），只要时间到了就移除
            queue.Dequeue();
        }
    }

    private void DequeueAndPlay<T>(List<PlaybackInfo> buffer, Queue<T> queue) where T : HitsoundNode
    {
        var node = queue.Dequeue();
        if (_audioCacheService.TryGetAudioByNode(node, out var cachedSound))
        {
            buffer.Add(new PlaybackInfo(cachedSound, node));
        }
    }
}