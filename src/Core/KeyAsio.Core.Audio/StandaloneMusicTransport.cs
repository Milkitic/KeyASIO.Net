using NAudio.Wave;

namespace KeyAsio.Core.Audio;

public sealed class StandaloneMusicTransport : IPlaybackClock, IAsyncDisposable
{
    private readonly IPlaybackEngine _playbackEngine;
    private readonly Lock _gate = new();

    private IMusicPlaybackSource? _source;
    private bool _ownsSource;
    private bool _isInMixer;

    public StandaloneMusicTransport(IPlaybackEngine playbackEngine)
    {
        _playbackEngine = playbackEngine;
    }

    public event Action<MusicTransportState>? StateChanged;

    public IMusicPlaybackSource? Source
    {
        get
        {
            lock (_gate)
            {
                return _source;
            }
        }
    }

    public MusicTransportState State { get; private set; } = MusicTransportState.Empty;
    public TimeSpan Position => Source?.Position ?? TimeSpan.Zero;
    public TimeSpan Duration => Source?.Duration ?? TimeSpan.Zero;
    public PlaybackRateState RateState => Source?.RateState ?? PlaybackRateState.Normal;
    public bool IsRunning => Source?.IsRunning ?? false;

    public async Task LoadAsync(IMusicPlaybackSource source, bool ownsSource = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        await ClearAsync(cancellationToken).ConfigureAwait(false);
        EnsureCompatibleFormat(source.WaveFormat);

        lock (_gate)
        {
            _source = source;
            _ownsSource = ownsSource;
            _isInMixer = false;
        }

        SetState(MusicTransportState.Ready);
    }

    public async Task PlayAsync(CancellationToken cancellationToken = default)
    {
        var source = GetRequiredSource();
        AddToMixer(source);
        await source.PlayAsync(cancellationToken).ConfigureAwait(false);
        SetState(MusicTransportState.Playing);
    }

    public async Task PauseAsync(CancellationToken cancellationToken = default)
    {
        var source = GetRequiredSource();
        await source.PauseAsync(cancellationToken).ConfigureAwait(false);
        RemoveFromMixer(source);
        SetState(MusicTransportState.Paused);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        var source = GetRequiredSource();
        await source.StopAsync(cancellationToken).ConfigureAwait(false);
        RemoveFromMixer(source);
        SetState(MusicTransportState.Stopped);
    }

    public Task SeekAsync(TimeSpan position, CancellationToken cancellationToken = default)
    {
        var source = GetRequiredSource();
        return source.SeekAsync(position, cancellationToken);
    }

    public Task SetPlaybackRateAsync(PlaybackRateState rateState, CancellationToken cancellationToken = default)
    {
        var source = GetRequiredSource();
        return source.SetPlaybackRateAsync(rateState, cancellationToken);
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        IMusicPlaybackSource? source;
        bool ownsSource;

        lock (_gate)
        {
            source = _source;
            ownsSource = _ownsSource;
            _source = null;
            _ownsSource = false;
        }

        if (source == null)
        {
            SetState(MusicTransportState.Empty);
            return;
        }

        await source.StopAsync(cancellationToken).ConfigureAwait(false);
        RemoveFromMixer(source);

        if (ownsSource)
        {
            await source.DisposeAsync().ConfigureAwait(false);
        }

        SetState(MusicTransportState.Empty);
    }

    public async ValueTask DisposeAsync()
    {
        await ClearAsync().ConfigureAwait(false);
    }

    private IMusicPlaybackSource GetRequiredSource()
    {
        lock (_gate)
        {
            return _source ?? throw new InvalidOperationException("No music source is loaded.");
        }
    }

    private void AddToMixer(IMusicPlaybackSource source)
    {
        lock (_gate)
        {
            if (_isInMixer) return;
            _playbackEngine.MusicMixer.AddMixerInput(source.Output);
            _isInMixer = true;
        }
    }

    private void RemoveFromMixer(IMusicPlaybackSource source)
    {
        lock (_gate)
        {
            if (!_isInMixer) return;
            _playbackEngine.MusicMixer.RemoveMixerInput(source.Output);
            _isInMixer = false;
        }
    }

    private void EnsureCompatibleFormat(WaveFormat sourceFormat)
    {
        var mixerFormat = _playbackEngine.MusicMixer.WaveFormat
                          ?? throw new InvalidOperationException("The playback engine music mixer is not started.");

        if (sourceFormat.SampleRate != mixerFormat.SampleRate ||
            sourceFormat.Channels != mixerFormat.Channels)
        {
            throw new ArgumentException(
                $"Music source format {sourceFormat} is not compatible with engine format {mixerFormat}.",
                nameof(sourceFormat));
        }
    }

    private void SetState(MusicTransportState state)
    {
        if (State == state) return;
        State = state;
        StateChanged?.Invoke(state);
    }
}
