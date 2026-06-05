using NAudio.Wave;

namespace KeyAsio.Core.Audio;

public interface IPlaybackRateProcessorFactory
{
    bool IsSupported { get; }

    IPlaybackRateProcessor Create(ISampleProvider source, PlaybackRateState initialState);
}
