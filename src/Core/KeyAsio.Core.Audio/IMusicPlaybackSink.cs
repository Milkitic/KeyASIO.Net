using NAudio.Wave;

namespace KeyAsio.Core.Audio;

public interface IMusicPlaybackSink
{
    WaveFormat? WaveFormat { get; }

    void AddInput(ISampleProvider input);

    void RemoveInput(ISampleProvider input);
}
