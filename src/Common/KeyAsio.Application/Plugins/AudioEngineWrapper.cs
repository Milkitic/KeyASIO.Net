using KeyAsio.Core.Audio;
using KeyAsio.Plugins.Contracts;
using NAudio.Wave;
using HostAudioFileReader = KeyAsio.Core.Audio.Wave.AudioFileReader;

namespace KeyAsio.Application.Plugins;

public sealed class AudioEngineWrapper : IAudioEngine
{
    private readonly IPlaybackEngine _engine;

    public AudioEngineWrapper(IPlaybackEngine engine)
    {
        _engine = engine;
    }

    public WaveFormat? OutputWaveFormat => _engine.CurrentDevice is null ? null : _engine.EngineWaveFormat;

    public IPluginAudioFile OpenAudioFile(string path) => new PluginAudioFile(path);

    public void AddMusicInput(ISampleProvider input) => _engine.MusicMixer.AddMixerInput(input);

    public void RemoveMusicInput(ISampleProvider input) => _engine.MusicMixer.RemoveMixerInput(input);

    private sealed class PluginAudioFile : IPluginAudioFile
    {
        private readonly HostAudioFileReader _reader;

        public PluginAudioFile(string path)
        {
            _reader = new HostAudioFileReader(path);
        }

        public WaveFormat WaveFormat => _reader.WaveFormat;
        public TimeSpan Position { get => _reader.CurrentTime; set => _reader.CurrentTime = value; }
        public TimeSpan Duration => _reader.TotalTime;

        public int Read(float[] buffer, int offset, int count) => _reader.Read(buffer, offset, count);

        public void Dispose() => _reader.Dispose();

        public ValueTask DisposeAsync() => _reader.DisposeAsync();
    }
}
