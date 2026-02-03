using KeyAsio.Core.Audio.SampleProviders;
using NAudio.Wave;

namespace KeyAsio.Core.Audio;

public interface IPlaybackEngine : IDisposable
{
    IWavePlayer? CurrentDevice { get; }
    DeviceDescription? CurrentDeviceDescription { get; }

    WaveFormat EngineWaveFormat { get; }
    WaveFormat SourceWaveFormat { get; }

    IMixingSampleProvider EffectMixer { get; }
    IMixingSampleProvider MusicMixer { get; }
    IMixingSampleProvider RootMixer { get; }
    ISampleProvider RootSampleProvider { get; }

    LimiterType LimiterType { get; set; }
    float MainVolume { get; set; }
    float EffectVolume { get; set; }
    float MusicVolume { get; set; }

    void StartDevice(DeviceDescription? deviceDescription, WaveFormat? waveFormat = null);
    void StopDevice();
}