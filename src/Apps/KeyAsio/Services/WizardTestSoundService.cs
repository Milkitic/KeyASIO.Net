using KeyAsio.Core.Audio;
using KeyAsio.Core.Audio.Caching;
using KeyAsio.Sync.Services;
using Microsoft.Extensions.Logging;

namespace KeyAsio.Services;

public interface IWizardTestSoundService
{
    bool IsPlaying { get; }

    void Start();

    void Stop();
}

public sealed class WizardTestSoundService : IWizardTestSoundService, IDisposable
{
    private const string TestSoundKey = "internal://dynamic/normal-hitnormal";

    private readonly AudioCacheManager _audioCacheManager;
    private readonly IPlaybackEngine _playbackEngine;
    private readonly SfxPlaybackService _sfxPlaybackService;
    private readonly ILogger<WizardTestSoundService> _logger;
    private readonly Lock _stateLock = new();

    private CancellationTokenSource? _playbackCts;

    public WizardTestSoundService(
        AudioCacheManager audioCacheManager,
        IPlaybackEngine playbackEngine,
        SfxPlaybackService sfxPlaybackService,
        ILogger<WizardTestSoundService> logger)
    {
        _audioCacheManager = audioCacheManager;
        _playbackEngine = playbackEngine;
        _sfxPlaybackService = sfxPlaybackService;
        _logger = logger;
    }

    public bool IsPlaying
    {
        get
        {
            lock (_stateLock)
            {
                return _playbackCts is { IsCancellationRequested: false };
            }
        }
    }

    public void Start()
    {
        lock (_stateLock)
        {
            StopCore();

            if (_playbackEngine.CurrentDevice is null)
            {
                throw new InvalidOperationException("Audio device must be active before starting the wizard test sound.");
            }

            var cachedAudio = _audioCacheManager.CreateDynamic(TestSoundKey, _playbackEngine.EngineWaveFormat);
            _playbackCts = new CancellationTokenSource();
            _sfxPlaybackService.PlayEffectsAudio(cachedAudio, 0.8f, 0f);
            _ = RunPlaybackLoopAsync(cachedAudio, _playbackCts.Token);
        }
    }

    public void Stop()
    {
        lock (_stateLock)
        {
            StopCore();
        }
    }

    private async Task RunPlaybackLoopAsync(CachedAudio cachedAudio, CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                _sfxPlaybackService.PlayEffectsAudio(cachedAudio, 0.8f, 0f);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Wizard test sound playback failed");
            Stop();
        }
    }

    private void StopCore()
    {
        _playbackCts?.Cancel();
        _playbackCts?.Dispose();
        _playbackCts = null;
    }

    public void Dispose() => Stop();
}
