using System.Runtime.CompilerServices;
using NAudio.Wave;

namespace KeyAsio.Core.Audio;

public sealed class SilentMusicPlaybackSource : IMusicPlaybackSource
{
    private readonly SilentSampleProvider _audioProvider;
    private readonly IPlaybackRateProcessorFactory _rateProcessorFactory;
    private readonly Lock _gate = new();

    private IPlaybackRateProcessor? _rateProcessor;
    private ISampleProvider _output;
    private bool _isRunning;

    private SilentMusicPlaybackSource(TimeSpan duration, WaveFormat waveFormat,
        IPlaybackRateProcessorFactory rateProcessorFactory)
    {
        _audioProvider = new SilentSampleProvider(duration, waveFormat);
        _rateProcessorFactory = rateProcessorFactory;
        _output = _audioProvider;
        WaveFormat = _audioProvider.WaveFormat;
    }

    public static SilentMusicPlaybackSource Create(TimeSpan duration, WaveFormat waveFormat,
        IPlaybackRateProcessorFactory? rateProcessorFactory = null)
    {
        ArgumentNullException.ThrowIfNull(waveFormat);
        if (duration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(duration));
        return new SilentMusicPlaybackSource(duration, waveFormat,
            rateProcessorFactory ?? NoPlaybackRateProcessorFactory.Instance);
    }

    public event Action<ISampleProvider, ISampleProvider>? OutputChanged;

    public WaveFormat WaveFormat { get; }
    public TimeSpan Duration => _audioProvider.Duration;
    public TimeSpan Position => _audioProvider.PlayTime;
    public PlaybackRateState RateState { get; private set; } = PlaybackRateState.Normal;
    public bool IsRunning => _isRunning;
    public ISampleProvider Output => _output;
    public bool SupportsPlaybackRateChange => _rateProcessorFactory.IsSupported;
    public bool IsLooping
    {
        get => _audioProvider.IsLooping;
        set => _audioProvider.IsLooping = value;
    }

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

    public Task SetPlaybackRateAsync(PlaybackRateState rateState,
        CancellationToken cancellationToken = default)
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
    }
}

internal sealed class SilentSampleProvider : ISampleProvider
{
    private readonly int _totalSamples;
    private readonly double _inverseSampleRate;
    private readonly int _channels;
    private readonly Lock _gate = new();
    private int _position;

    public SilentSampleProvider(TimeSpan duration, WaveFormat sourceFormat)
    {
        _channels = sourceFormat.Channels;
        _inverseSampleRate = 1.0 / sourceFormat.SampleRate;
        _totalSamples = (int)(duration.TotalSeconds * sourceFormat.SampleRate) * _channels;
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sourceFormat.SampleRate,
            sourceFormat.Channels);
    }

    public WaveFormat WaveFormat { get; }
    public bool IsLooping { get; set; }

    public TimeSpan Duration =>
        TimeSpan.FromSeconds((double)_totalSamples / (WaveFormat.SampleRate * _channels));

    public TimeSpan PlayTime
    {
        get
        {
            lock (_gate)
            {
                return TimeSpan.FromSeconds(_position * _inverseSampleRate / _channels);
            }
        }
        set
        {
            lock (_gate)
            {
                _position = (int)(value.TotalSeconds * WaveFormat.SampleRate * _channels);
            }
        }
    }

    public int Read(float[] buffer, int offset, int count)
    {
        if (count == 0) return 0;

        lock (_gate)
        {
            if (IsLooping && _totalSamples > 0)
            {
                Array.Clear(buffer, offset, count);
                _position = (_position + count) % _totalSamples;
                return count;
            }

            var remaining = _totalSamples - _position;
            if (remaining <= 0) return 0;

            var toRead = Math.Min(remaining, count);
            Array.Clear(buffer, offset, toRead);
            _position += toRead;
            return toRead;
        }
    }
}
