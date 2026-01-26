using System.Diagnostics;
using System.Runtime.CompilerServices;
using KeyAsio.Core.Audio;
using KeyAsio.Core.Audio.Caching;
using KeyAsio.Shared.Hitsounds.Playback;
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
    private readonly IPlaybackEngine _playbackEngine;
    private readonly GameplayAudioService _gameplayAudioService;
    private readonly GameplaySessionManager _gameplaySessionManager;

    private Queue<SampleEvent> _hitQueue = new();
    private Queue<PlaybackEvent> _playbackQueue = new();

    private long _lastDonTime = 0;
    private long _lastKatTime = 0;
    private const double MinIntervalMs = 30.0;

    public TaikoHitsoundSequencer(ILogger<TaikoHitsoundSequencer> logger,
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

    public int KeyThresholdMilliseconds { get; set; } = 100;

    public void SeekTo(int playTime)
    {
        _hitQueue = new Queue<SampleEvent>(
            _gameplaySessionManager.KeyList.Where(k => k.Offset >= playTime - KeyThresholdMilliseconds)
        );
        _playbackQueue = new Queue<PlaybackEvent>(_gameplaySessionManager.PlaybackList
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

        // 获取参考 Note 用于空打音效
        PlaybackEvent? refNode = null;

        if (_hitQueue.TryPeek(out var headNode))
        {
            refNode = headNode;
        }
        else if (_playbackQueue.TryPeek(out var pbNode))
        {
            refNode = pbNode;
        }

        bool soundPlayed = false;

        // 借用 mania 的逻辑来处理 Taiko 的判定，Chord 实际上不应存在

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

                DequeueAndPlay(buffer, _hitQueue);
                continue;
            }

            // --- 情况 2: 音符已彻底过期 (Missed) ---
            if (diff > KeyThresholdMilliseconds)
            {
                _hitQueue.Dequeue(); // 移除过期音符
                // 更新参考 Note
                if (_hitQueue.TryPeek(out var nextNode)) refNode = nextNode;
                continue;
            }

            // --- 情况 3: 点击太早 (Too Early) ---
            if (diff < -KeyThresholdMilliseconds)
            {
                break;
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
                if (ShouldPlaySound(isDonInput))
                {
                    DequeueAndPlay(buffer, _hitQueue);
                    UpdateLastPlayTime(isDonInput);
                    soundPlayed = true;
                }
                else
                {
                    // 防抖生效：只移除不播放
                    _hitQueue.Dequeue();
                    soundPlayed = true; // 视为已处理，避免触发后续空打逻辑
                }
            }
            else
            {
                // 类型不匹配
                // 消耗掉音符，不播放。
                _hitQueue.Dequeue();
                refNode = node; // 使用这个 Miss 的 Note 作为空打声音的参考

                // 既然这次点击消耗了这个音符（判定为 Miss），那么我们就不能再用这次点击去匹配其他音符了。
                break;
            }
        }

        if (!soundPlayed && refNode != null)
        {
            if (ShouldPlaySound(isDonInput))
            {
                if (GetFallbackAudio(refNode, isDonInput, out var cachedAudio))
                {
                    buffer.Add(new PlaybackInfo(cachedAudio!, refNode));
                    UpdateLastPlayTime(isDonInput);
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool ShouldPlaySound(bool isDon)
    {
        long currentTimestamp = Stopwatch.GetTimestamp();
        long lastTime = isDon ? _lastDonTime : _lastKatTime;
        double elapsedMs = (currentTimestamp - lastTime) * 1000.0 / Stopwatch.Frequency;
        return elapsedMs >= MinIntervalMs;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateLastPlayTime(bool isDon)
    {
        if (isDon) _lastDonTime = Stopwatch.GetTimestamp();
        else _lastKatTime = Stopwatch.GetTimestamp();
    }

    private bool GetFallbackAudio(PlaybackEvent node, bool isDon, out CachedAudio? cachedAudio)
    {
        cachedAudio = null;
        if (node.Filename == null) return false;

        var originalFilename = Path.GetFileNameWithoutExtension(node.Filename);
        string newFilename = originalFilename;

        if (isDon)
        {
            // 确保移除 clap/whistle 等元素，只保留 normal
            // 这里假设文件名格式是标准的，或者包含这些关键字
            newFilename = newFilename
                .Replace("hitclap", "hitnormal")
                .Replace("hitwhistle", "hitnormal")
                .Replace("hitfinish", "hitnormal");
        }
        else
        {
            // 如果是 Kat，优先使用 clap
            // 如果原文件名是 hitwhistle，保留（Whistle 也是 Kat）
            if (!newFilename.Contains("hitwhistle"))
            {
                newFilename = newFilename
                    .Replace("hitnormal", "hitclap")
                    .Replace("hitfinish", "hitclap");
            }
        }

        if (_gameplayAudioService.TryGetCachedAudio(newFilename, out cachedAudio))
            return true;

        string sampleSet = "normal";
        if (originalFilename.Contains("soft")) sampleSet = "soft";
        else if (originalFilename.Contains("drum")) sampleSet = "drum";

        string suffix = isDon ? "hitnormal" : "hitclap";

        if (_gameplayAudioService.TryGetCachedAudio($"taiko-{sampleSet}-{suffix}", out cachedAudio))
            return true;

        if (_gameplayAudioService.TryGetCachedAudio($"{sampleSet}-{suffix}", out cachedAudio))
            return true;

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsEngineReady()
    {
        if (_playbackEngine.CurrentDevice == null)
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

    public void FillAudioList(IReadOnlyList<PlaybackEvent> nodeList, List<SampleEvent> keyList,
        List<PlaybackEvent> playbackList)
    {
        var secondaryCache = new List<SampleEvent>();
        var options = _appSettings.Sync;

        foreach (var hitsoundNode in nodeList)
        {
            if (hitsoundNode is not SampleEvent playableNode)
            {
                if (hitsoundNode is ControlEvent controlNode &&
                    controlNode.ControlEventType != ControlEventType.Balance &&
                    controlNode.ControlEventType != ControlEventType.None &&
                    controlNode.ControlEventType != ControlEventType.LoopStart &&
                    controlNode.ControlEventType != ControlEventType.LoopStop &&
                    !options.Filters.DisableSliderTicksAndSlides)
                {
                    playbackList.Add(controlNode);
                }

                continue;
            }

            switch (playableNode.Layer)
            {
                case SampleLayer.Primary:
                    CheckSecondary();
                    secondaryCache.Clear();
                    keyList.Add(playableNode);
                    break;
                case SampleLayer.Secondary:
                    if (options.Playback.TailPlaybackBehavior == SliderTailPlaybackBehavior.Normal)
                        playbackList.Add(playableNode);
                    else if (options.Playback.TailPlaybackBehavior == SliderTailPlaybackBehavior.KeepReverse)
                        secondaryCache.Add(playableNode);
                    break;
                case SampleLayer.Effects:
                    if (!options.Filters.DisableSliderTicksAndSlides)
                        playbackList.Add(playableNode);
                    break;
                case SampleLayer.Sampling:
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
        where T : PlaybackEvent
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

    private void DequeueAndPlay<T>(List<PlaybackInfo> buffer, Queue<T> queue) where T : PlaybackEvent
    {
        var node = queue.Dequeue();
        if (_gameplayAudioService.TryGetAudioByNode(node, out var cachedSound))
        {
            buffer.Add(new PlaybackInfo(cachedSound, node));
        }
    }
}