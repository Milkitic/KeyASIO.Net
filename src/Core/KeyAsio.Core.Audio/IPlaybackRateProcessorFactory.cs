using NAudio.Wave;

namespace KeyAsio.Core.Audio;

public interface IPlaybackRateProcessorFactory
{
    IPlaybackRateProcessor Create(ISampleProvider source, PlaybackRateState initialState);
}
