using NAudio.Wave;

namespace KeyAsio.Plugins.Contracts;

/// <summary>
/// Narrow host audio capabilities available to plugins. Mixer and decoder
/// implementations remain owned by the host.
/// </summary>
public interface IAudioEngine
{
    WaveFormat OutputWaveFormat { get; }

    IPluginAudioFile OpenAudioFile(string path);

    void AddMusicInput(ISampleProvider input);

    void RemoveMusicInput(ISampleProvider input);
}

public interface IPluginAudioFile : ISampleProvider, IDisposable, IAsyncDisposable
{
    TimeSpan Position { get; set; }

    TimeSpan Duration { get; }
}

public interface ISeekableAudioSampleProvider : ISampleProvider
{
    string ResourceId { get; }

    TimeSpan Position { get; set; }
}
