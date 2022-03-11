using KeyAsio.Net.Models;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using NAudio.Wave;

namespace KeyAsio.Net.Audio;

public static class DeviceProvider
{
    private static readonly MMDeviceEnumerator MmDeviceEnumerator;
    private static IReadOnlyList<DeviceDescription>? _cacheList;

    static DeviceProvider()
    {
        MmDeviceEnumerator = new MMDeviceEnumerator();
        AppDomain.CurrentDomain.ProcessExit += (_, _) => MmDeviceEnumerator.Dispose();

        if (Environment.OSVersion.Version.Major < 6) return;
        var mmNotificationClient = new MmNotificationClient();
        GC.KeepAlive(mmNotificationClient);
        MmDeviceEnumerator.RegisterEndpointNotificationCallback(mmNotificationClient);
    }

    public static IWavePlayer? CurrentDevice { get; private set; }

    public static IWavePlayer CreateDevice(out DeviceDescription actualDeviceInfo, DeviceDescription? description = null)
    {
        description ??= MmDeviceEnumerator.HasDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia)
            ? DeviceDescription.WasapiDefault
            : DeviceDescription.DirectSoundDefault;

        IWavePlayer? device = null;
        Execute.OnUiThread(() =>
        {
            switch (description.WavePlayerType)
            {
                case WavePlayerType.DirectSound:
                    device = TryCreateDirectSoundOrDefault(description);
                    return;
                case WavePlayerType.WASAPI:
                    device = TryCreateWasapiOrDefault(description);
                    return;
                case WavePlayerType.ASIO:
                    device = TryCreateAsioOrDefault(description);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        });

        CurrentDevice = device!;
        actualDeviceInfo = description;
        return device!;
    }

    public static IReadOnlyList<DeviceDescription> GetCachedAvailableDevices()
    {
        if (_cacheList != null)
        {
            return _cacheList;
        }

        return _cacheList = EnumerateDeviceDescriptions().ToArray();
    }

    private static IWavePlayer TryCreateDirectSoundOrDefault(DeviceDescription description)
    {
        IWavePlayer device;
        if (description.Equals(DeviceDescription.DirectSoundDefault))
        {
            device = new DirectSoundOut(description.Latency);
        }
        else
        {
            device = new DirectSoundOut(Guid.Parse(description.DeviceId!), description.Latency);
        }

        return device;
    }

    private static IWavePlayer TryCreateWasapiOrDefault(DeviceDescription description)
    {
        if (!description.Equals(DeviceDescription.WasapiDefault))
        {
            try
            {
                var mmDevice = MmDeviceEnumerator.GetDevice(description.DeviceId);
                IWavePlayer device = new WasapiOut(mmDevice,
                    description.IsExclusive ? AudioClientShareMode.Exclusive : AudioClientShareMode.Shared,
                    true,
                    description.Latency);
                return device;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while creating WASAPI device {description.DeviceId}: " +
                                  ex.Message);
            }
        }

        return new WasapiOut(AudioClientShareMode.Shared, description.Latency);
    }

    private static IWavePlayer TryCreateAsioOrDefault(DeviceDescription description)
    {
        IWavePlayer device = new AsioOut(description.DeviceId);
        return device;
    }

    private static IEnumerable<DeviceDescription> EnumerateDeviceDescriptions()
    {
        foreach (var dev in DirectSoundOut.Devices)
        {
            DeviceDescription? info = null;
            try
            {
                info = new DeviceDescription
                {
                    DeviceId = dev.Guid.ToString(),
                    FriendlyName = dev.Description,
                    WavePlayerType = WavePlayerType.DirectSound
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error while enumerating DirectSoundOut device: {0}", ex.Message);
            }

            if (info != null)
            {
                yield return info;
            }
        }

        yield return DeviceDescription.WasapiDefault;
        foreach (var wasapi in MmDeviceEnumerator.EnumerateAudioEndPoints(DataFlow.All, DeviceState.All))
        {
            DeviceDescription? info = null;
            try
            {
                if (wasapi.DataFlow != DataFlow.Render || wasapi.State != DeviceState.Active) continue;
                info = new DeviceDescription
                {
                    DeviceId = wasapi.ID,
                    FriendlyName = wasapi.FriendlyName,
                    WavePlayerType = WavePlayerType.WASAPI
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error while enumerating WASAPI device: {0}", ex.Message);
            }

            if (info != null)
            {
                yield return info;
            }
        }

        foreach (var asio in AsioOut.GetDriverNames())
        {
            DeviceDescription? info = null;
            try
            {
                info = new DeviceDescription
                {
                    DeviceId = asio,
                    FriendlyName = asio,
                    WavePlayerType = WavePlayerType.ASIO
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error while enumerating ASIO device: {0}", ex.Message);
            }

            if (info != null)
            {
                yield return info;
            }
        }
    }

    private class MmNotificationClient : IMMNotificationClient
    {
        public void OnDeviceStateChanged(string deviceId, DeviceState newState) => _cacheList = null;
        public void OnDeviceAdded(string pwstrDeviceId) => _cacheList = null;
        public void OnDeviceRemoved(string deviceId) => _cacheList = null;
        public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId) => _cacheList = null;
        public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key) { }
    }
}