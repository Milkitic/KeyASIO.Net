using NAudio.Wave;

namespace KeyAsio.Core.Audio;

public interface IAudioDeviceManager : IDisposable
{
    Task<IReadOnlyList<DeviceDescription>> GetCachedAvailableDevicesAsync();
    void ClearCache();

    (IWavePlayer Player, DeviceDescription ActualDescription) CreateDevice(
        DeviceDescription? description = null,
        SynchronizationContext? context = null);
}