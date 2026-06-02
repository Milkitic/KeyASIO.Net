using NAudio.Wave;

namespace KeyAsio.Core.Audio;

public interface IMusicPlaybackSource : IPlaybackClock, IAsyncDisposable
{
    event Action<ISampleProvider, ISampleProvider>? OutputChanged;

    WaveFormat WaveFormat { get; }
    TimeSpan Duration { get; }
    ISampleProvider Output { get; }

    Task PlayAsync(CancellationToken cancellationToken = default);
    Task PauseAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    Task SeekAsync(TimeSpan position, CancellationToken cancellationToken = default);
    Task SetPlaybackRateAsync(PlaybackRateState rateState, CancellationToken cancellationToken = default);
}
