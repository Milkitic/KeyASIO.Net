namespace KeyAsio.Core.Audio;

public interface IPlaybackClock
{
    TimeSpan Position { get; }
    PlaybackRateState RateState { get; }
    bool IsRunning { get; }
}
