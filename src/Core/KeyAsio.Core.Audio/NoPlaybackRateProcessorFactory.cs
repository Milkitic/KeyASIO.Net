using NAudio.Wave;

namespace KeyAsio.Core.Audio;

public sealed class NoPlaybackRateProcessorFactory : IPlaybackRateProcessorFactory
{
    public static NoPlaybackRateProcessorFactory Instance { get; } = new();

    private NoPlaybackRateProcessorFactory()
    {
    }

    public bool IsSupported => false;

    public IPlaybackRateProcessor Create(ISampleProvider source, PlaybackRateState initialState)
    {
        throw new NotSupportedException("Playback rate changes require an IPlaybackRateProcessorFactory implementation.");
    }
}
