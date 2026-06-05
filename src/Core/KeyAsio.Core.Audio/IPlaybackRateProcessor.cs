using NAudio.Wave;

namespace KeyAsio.Core.Audio;

public interface IPlaybackRateProcessor : ISampleProvider, IDisposable
{
    PlaybackRateState RateState { get; set; }
    void Reposition();
}
