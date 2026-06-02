using KeyAsio.Core.Audio.Caching;
using KeyAsio.Core.Audio.SampleProviders;
using NAudio.Wave;

namespace KeyAsio.Core.Audio;

public sealed class AudioFileMusicPlaybackSource : IMusicPlaybackSource
{
    private readonly CachedAudio _cachedAudio;
    private readonly IPlaybackRateProcessorFactory _rateProcessorFactory;
    private readonly Lock _gate = new();

    private CachedAudioProvider _audioProvider;
    private IPlaybackRateProcessor? _rateProcessor;
    private ISampleProvider _output;
    private bool _isRunning;

    private AudioFileMusicPlaybackSource(CachedAudio cachedAudio, IPlaybackRateProcessorFactory rateProcessorFactory)
    {
        _cachedAudio = cachedAudio;
        _rateProcessorFactory = rateProcessorFactory;
        _audioProvider = CreateAudioProvider(cachedAudio);
        _output = _audioProvider;
        WaveFormat = _audioProvider.WaveFormat;
    }

    public event Action<ISampleProvider, ISampleProvider>? OutputChanged;

    public static async Task<AudioFileMusicPlaybackSource> CreateAsync(
        AudioCacheManager audioCacheManager,
        string filePath,
        WaveFormat waveFormat,
        IPlaybackRateProcessorFactory? rateProcessorFactory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(audioCacheManager);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(waveFormat);

        cancellationToken.ThrowIfCancellationRequested();
        var (cachedAudio, status) = await audioCacheManager.GetOrCreateOrEmptyFromFileAsync(filePath, waveFormat)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        if (cachedAudio == null || status == CacheGetStatus.Failed)
        {
            throw new InvalidOperationException($"Failed to load music file: {filePath}");
        }

        return new AudioFileMusicPlaybackSource(cachedAudio,
            rateProcessorFactory ?? NoPlaybackRateProcessorFactory.Instance);
    }

    public WaveFormat WaveFormat { get; }
    public TimeSpan Duration => _cachedAudio.Duration;
    public TimeSpan Position => _audioProvider.PlayTime;
    public PlaybackRateState RateState { get; private set; } = PlaybackRateState.Normal;
    public bool IsRunning => _isRunning;
    public ISampleProvider Output => _output;
    public bool SupportsPlaybackRateChange => _rateProcessorFactory.IsSupported;

    public Task PlayAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _isRunning = true;
        return Task.CompletedTask;
    }

    public Task PauseAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _isRunning = false;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _isRunning = false;
        return SeekAsync(TimeSpan.Zero, cancellationToken);
    }

    public Task SeekAsync(TimeSpan position, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            _audioProvider.PlayTime = position;
            _rateProcessor?.Reposition();
        }

        return Task.CompletedTask;
    }

    public Task SetPlaybackRateAsync(PlaybackRateState rateState, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (RateState.Equals(rateState)) return Task.CompletedTask;

            var oldOutput = _output;
            RateState = rateState;

            if (rateState.Rate.Equals(1.0f))
            {
                _rateProcessor?.Dispose();
                _rateProcessor = null;
                _output = _audioProvider;
            }
            else
            {
                if (!_rateProcessorFactory.IsSupported)
                {
                    throw new NotSupportedException(
                        "Playback rate changes require an IPlaybackRateProcessorFactory implementation.");
                }

                if (_rateProcessor == null)
                {
                    _rateProcessor = _rateProcessorFactory.Create(_audioProvider, rateState);
                    _output = _rateProcessor;
                }
                else
                {
                    _rateProcessor.RateState = rateState;
                }
            }

            if (!ReferenceEquals(oldOutput, _output))
            {
                OutputChanged?.Invoke(oldOutput, _output);
            }
        }

        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        _rateProcessor?.Dispose();
        _audioProvider.Reset();
    }

    private static CachedAudioProvider CreateAudioProvider(CachedAudio cachedAudio)
    {
        return new CachedAudioProvider(cachedAudio)
        {
            ExcludeFromPool = true
        };
    }
}
