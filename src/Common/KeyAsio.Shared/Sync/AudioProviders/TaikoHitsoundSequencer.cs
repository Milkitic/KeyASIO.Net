using System.Runtime.CompilerServices;
using Coosu.Beatmap.Extensions.Playback;
using KeyAsio.Core.Audio;
using KeyAsio.Shared.Models;
using KeyAsio.Shared.Sync.Services;
using Microsoft.Extensions.Logging;

namespace KeyAsio.Shared.Sync.AudioProviders;

public class TaikoHitsoundSequencer : IHitsoundSequencer
{
    private const int AudioLatencyTolerance = 200;

    private readonly ILogger<TaikoHitsoundSequencer> _logger;
    private readonly AppSettings _appSettings;
    private readonly SyncSessionContext _syncSessionContext;
    private readonly AudioEngine _audioEngine;
    private readonly GameplayAudioService _gameplayAudioService;
    private readonly GameplaySessionManager _gameplaySessionManager;

    private Queue<PlayableNode> _hitQueue = new();
    private Queue<HitsoundNode> _playbackQueue = new();

    public TaikoHitsoundSequencer(ILogger<TaikoHitsoundSequencer> logger,
        AppSettings appSettings,
        SyncSessionContext syncSessionContext,
        AudioEngine audioEngine,
        GameplayAudioService gameplayAudioService,
        GameplaySessionManager gameplaySessionManager)
    {
        _logger = logger;
        _appSettings = appSettings;
        _syncSessionContext = syncSessionContext;
        _audioEngine = audioEngine;
        _gameplayAudioService = gameplayAudioService;
        _gameplaySessionManager = gameplaySessionManager;
    }

    public int KeyThresholdMilliseconds { get; set; } = 100;

    public void SeekTo(int playTime)
    {
        _hitQueue = new Queue<PlayableNode>(
            _gameplaySessionManager.KeyList.Where(k => k.Offset >= playTime - KeyThresholdMilliseconds)
        );
        _playbackQueue = new Queue<HitsoundNode>(_gameplaySessionManager.PlaybackList
            .Where(k => k.Offset >= playTime));
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

        var playTime = _syncSessionContext.PlayTime;
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

        var playTime = _syncSessionContext.PlayTime;

        // 用于处理同组音符（堆叠/Chord）
        bool hasHit = false;
        Guid? currentGroupGuid = null;

        // Taiko Logic:
        // KeyTotal usually is 4 (KDDK)
        // 0: Kat (Left Rim)
        // 1: Don (Left Center)
        // 2: Don (Right Center)
        // 3: Kat (Right Rim)
        bool isDonInput = keyIndex == 1 || keyIndex == 2;
        bool isKatInput = keyIndex == 0 || keyIndex == 3;

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

                // 同组音符，检查类型是否匹配
                // 在 Taiko 中，同组音符通常是 Big Note 的一部分，或者是重叠音符
                // 这里我们假设如果之前的音符匹配了，同组的也应该匹配（或者忽略类型？）
                // 为了简单起见，我们对同组音符也进行类型检查，或者直接播放（如果是 Big Note，需要两个键，这里可能需要更复杂的逻辑）
                // 目前简化处理：直接播放
                DequeueAndPlay(buffer, _hitQueue);
                continue;
            }

            // --- 情况 2: 音符已彻底过期 (Missed) ---
            if (diff > KeyThresholdMilliseconds)
            {
                _hitQueue.Dequeue(); // 移除过期音符
                continue;
            }

            // --- 情况 3: 点击太早 (Too Early) ---
            if (diff < -KeyThresholdMilliseconds)
            {
                return;
            }

            // --- 情况 4: 命中判定窗口 (Hit) ---
            
            // Check Note Type
            // Filename check is a heuristic
            var filename = node.Filename ?? "";
            bool isKatNode = filename.Contains("clap", StringComparison.OrdinalIgnoreCase) || 
                             filename.Contains("whistle", StringComparison.OrdinalIgnoreCase);
            bool isDonNode = !isKatNode;

            bool typeMatch = (isDonInput && isDonNode) || (isKatInput && isKatNode);

            if (typeMatch)
            {
                // 记录状态，标记为已命中
                hasHit = true;
                currentGroupGuid = node.Guid;

                // 播放并移除
                DequeueAndPlay(buffer, _hitQueue);
            }
            else
            {
                // 类型不匹配
                // 在 osu!taiko 中，打错颜色是 Miss。
                // 我们应该消耗掉这个音符，但是不播放声音（或者播放 Miss 音效，但这里没有 Miss 音效）
                // 并且我们应该停止处理后续音符（因为这次点击已经被消耗了）
                
                // 但是，如果这是 Big Note，可能需要特殊的逻辑。
                // 暂时假设：消耗掉音符，不播放。
                
                _hitQueue.Dequeue();
                
                // 既然这次点击消耗了这个音符（判定为 Miss），那么我们就不能再用这次点击去匹配其他音符了。
                return;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsEngineReady()
    {
        if (_audioEngine.CurrentDevice == null)
        {
            _logger.LogWarning("Engine not ready.");
            return false;
        }

        if (!_syncSessionContext.IsStarted)
        {
            _logger.LogInformation("Game hasn't started.");
            return false;
        }

        return true;
    }

    public void FillAudioList(IReadOnlyList<HitsoundNode> nodeList, List<PlayableNode> keyList,
        List<HitsoundNode> playbackList)
    {
        var secondaryCache = new List<PlayableNode>();
        var options = _appSettings.Sync;

        foreach (var hitsoundNode in nodeList)
        {
            if (hitsoundNode is not PlayableNode playableNode)
            {
                if (hitsoundNode is ControlNode controlNode &&
                    controlNode.ControlType != ControlType.ChangeBalance &&
                    controlNode.ControlType != ControlType.None &&
                    !options.Filters.DisableSliderTicksAndSlides)
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
                    if (options.Playback.TailPlaybackBehavior == SliderTailPlaybackBehavior.Normal)
                        playbackList.Add(playableNode);
                    else if (options.Playback.TailPlaybackBehavior == SliderTailPlaybackBehavior.KeepReverse)
                        secondaryCache.Add(playableNode);
                    break;
                case PlayablePriority.Effects:
                    if (!options.Filters.DisableSliderTicksAndSlides)
                        playbackList.Add(playableNode);
                    break;
                case PlayablePriority.Sampling:
                    if (!options.Filters.DisableStoryboardSamples)
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
                if (_gameplayAudioService.TryGetAudioByNode(node, out var cachedSound))
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
        if (_gameplayAudioService.TryGetAudioByNode(node, out var cachedSound))
        {
            buffer.Add(new PlaybackInfo(cachedSound, node));
        }
    }
}
