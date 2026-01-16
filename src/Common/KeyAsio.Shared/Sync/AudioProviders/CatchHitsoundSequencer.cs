using System.Runtime.CompilerServices;
using KeyAsio.Core.Audio;
using KeyAsio.Shared.Hitsounds.Playback;
using KeyAsio.Shared.Models;
using KeyAsio.Shared.Sync.Services;
using Microsoft.Extensions.Logging;

namespace KeyAsio.Shared.Sync.AudioProviders;

public class CatchHitsoundSequencer : IHitsoundSequencer
{
    private const int AudioLatencyTolerance = 200;

    private readonly ILogger<CatchHitsoundSequencer> _logger;
    private readonly AppSettings _appSettings;
    private readonly SyncSessionContext _syncSessionContext;
    private readonly AudioEngine _audioEngine;
    private readonly GameplayAudioService _gameplayAudioService;
    private readonly GameplaySessionManager _gameplaySessionManager;

    private Queue<SampleEvent> _hitQueue = new();
    private Queue<PlaybackEvent> _playbackQueue = new();

    public CatchHitsoundSequencer(ILogger<CatchHitsoundSequencer> logger,
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
        _hitQueue = new Queue<SampleEvent>(
            _gameplaySessionManager.KeyList.Where(k => k.Offset >= playTime - KeyThresholdMilliseconds)
        );
        _playbackQueue = new Queue<PlaybackEvent>(_gameplaySessionManager.PlaybackList
            .Where(k => k.Offset >= playTime));
    }

    public void ProcessAutoPlay(List<PlaybackInfo> buffer, bool processHitQueueAsAuto)
    {
        if (!IsEngineReady()) return;

        var playTime = _syncSessionContext.PlayTime;

        // In Catch mode (Auto-Play approach), we treat hit objects as auto-played sounds.
        // Regardless of whether it's replay/auto or manual play, we play the hit sounds.

        // Process background sounds (slider ticks, etc.)
        ProcessTimeBasedQueue(buffer, _playbackQueue, playTime);

        // Process hit objects (fruits, drops)
        ProcessTimeBasedQueue(buffer, _hitQueue, playTime);
    }

    public void ProcessInteraction(List<PlaybackInfo> buffer, int keyIndex, int keyTotal)
    {
        // Catch mode (Auto-Play approach) does not require user interaction to trigger sounds.
        // Sounds are triggered automatically based on time.
    }

    public void FillAudioList(IReadOnlyList<PlaybackEvent> nodeList, List<SampleEvent> keyList,
        List<PlaybackEvent> playbackList)
    {
        // Use logic similar to Standard mode
        var secondaryCache = new List<SampleEvent>();
        var options = _appSettings.Sync;

        foreach (var hitsoundNode in nodeList)
        {
            if (hitsoundNode is not SampleEvent playableNode)
            {
                if (hitsoundNode is ControlEvent controlEvent &&
                    controlEvent.ControlEventType != ControlEventType.Balance &&
                    controlEvent.ControlEventType != ControlEventType.None &&
                    !options.Filters.DisableSliderTicksAndSlides)
                {
                    playbackList.Add(controlEvent);
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
                case SampleLayer.Sampling:
                    if (!options.Filters.DisableStoryboardSamples)
                    {
                        playbackList.Add(playableNode);
                    }

                    break;
            }
        }

        CheckSecondary();

        void CheckSecondary()
        {
            if (secondaryCache.Count > 0)
            {
                if (options.Playback.TailPlaybackBehavior == SliderTailPlaybackBehavior.KeepReverse)
                {
                    playbackList.AddRange(secondaryCache);
                }

                secondaryCache.Clear();
            }
        }
    }

    private void ProcessTimeBasedQueue<T>(List<PlaybackInfo> buffer, Queue<T> queue, int playTime)
        where T : PlaybackEvent
    {
        while (queue.TryPeek(out var node))
        {
            if (playTime < node.Offset)
            {
                // Not yet time
                break;
            }

            // Only play if within tolerance
            if (playTime < node.Offset + AudioLatencyTolerance)
            {
                if (_gameplayAudioService.TryGetAudioByNode(node, out var cachedSound))
                {
                    buffer.Add(new PlaybackInfo(cachedSound, node));
                }
            }

            // Dequeue regardless of whether played (played, timed out, or missing resource)
            queue.Dequeue();
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
}