using NAudio.Wave;

namespace KeyAsio.Core.Audio;

public interface IPlaybackEngine : IDisposable
{
    void StartDevice(DeviceDescription? deviceDescription, WaveFormat? waveFormat = null);
    void StopDevice();
    DeviceDescription? CurrentDeviceDescription { get; }
    IWavePlayer? CurrentDevice { get; }
}