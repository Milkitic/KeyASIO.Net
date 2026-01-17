using Microsoft.Extensions.Logging;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using NAudio.Wave;
using NAudio.Wave.Asio;

namespace KeyAsio.Core.Audio;

public sealed class AudioDeviceManager : IDisposable
{
    private readonly ILogger<AudioDeviceManager> _logger;
    private readonly MMDeviceEnumerator _mmDeviceEnumerator;
    private readonly MmNotificationClient _mmNotificationClient;

    private bool _disposed;
    private Lazy<Task<IReadOnlyList<DeviceDescription>>> _cachedDevices;

    public AudioDeviceManager(ILogger<AudioDeviceManager> logger)
    {
        _logger = logger;
        if (Environment.OSVersion.Version.Major < 6)
        {
            throw new NotSupportedException("This functionality is only supported on Windows Vista or newer.");
        }

        _mmDeviceEnumerator = new MMDeviceEnumerator();
        _cachedDevices = CreateLazyDeviceListAsync();

        _mmNotificationClient = new MmNotificationClient(this);
        _mmDeviceEnumerator.RegisterEndpointNotificationCallback(_mmNotificationClient);
    }

    public (IWavePlayer Player, DeviceDescription ActualDescription) CreateDevice(
        DeviceDescription? description = null,
        SynchronizationContext? context = null)
    {
        (IWavePlayer Player, DeviceDescription ActualDescription) result = default; // 这样声明

        if (context != null)
        {
            context.Send(_ => result = CreationCore(description), null);
        }
        else
        {
            result = CreationCore(description);
        }

        return result;
    }

    public Task<IReadOnlyList<DeviceDescription>> GetCachedAvailableDevicesAsync()
    {
        return _cachedDevices.Value;
    }

    public void ClearCache()
    {
        _cachedDevices = CreateLazyDeviceListAsync();
    }

    private Lazy<Task<IReadOnlyList<DeviceDescription>>> CreateLazyDeviceListAsync()
    {
        return new Lazy<Task<IReadOnlyList<DeviceDescription>>>(
            () => Task.Run(IReadOnlyList<DeviceDescription> () => EnumerateAllDevices().ToArray()),
            LazyThreadSafetyMode.ExecutionAndPublication
        );
    }

    private (IWavePlayer Player, DeviceDescription ActualDescription) CreationCore(DeviceDescription? description)
    {
        description ??= _mmDeviceEnumerator.HasDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia)
            ? DeviceDescription.WasapiDefault
            : DeviceDescription.DirectSoundDefault;

        if (description.WavePlayerType == WavePlayerType.ASIO)
        {
            var (device, desc) = CreateAsio(description);
            return (device, desc);
        }

        IWavePlayer wavePlayer = description.WavePlayerType switch
        {
            WavePlayerType.DirectSound => CreateDirectSound(description),
            WavePlayerType.WASAPI => CreateWasapi(description),
            _ => throw new ArgumentOutOfRangeException()
        };
        return (wavePlayer, description);
    }

    private DirectSoundOut CreateDirectSound(DeviceDescription description)
    {
        var device = DeviceComparer.Instance.Equals(description, DeviceDescription.DirectSoundDefault)
            ? new DirectSoundOut(description.Latency)
            : new DirectSoundOut(Guid.Parse(description.DeviceId!), description.Latency);

        return device;
    }

    private WasapiOut CreateWasapi(DeviceDescription description)
    {
        if (DeviceComparer.Instance.Equals(description, DeviceDescription.WasapiDefault))
        {
            return CreateDefaultWasapi(description);
        }

        try
        {
            var mmDevice = _mmDeviceEnumerator.GetDevice(description.DeviceId);
            var device = new WasapiOut(mmDevice,
                description.IsExclusive ? AudioClientShareMode.Exclusive : AudioClientShareMode.Shared,
                true,
                description.Latency);
            return device;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while creating WASAPI device {DeviceId}. Fallbacking to default device.",
                description.DeviceId);
            return CreateDefaultWasapi(description);
        }
    }

    private static WasapiOut CreateDefaultWasapi(DeviceDescription description)
    {
        return new WasapiOut(description.IsExclusive ? AudioClientShareMode.Exclusive : AudioClientShareMode.Shared,
            description.Latency);
    }

    private (AsioOut Device, DeviceDescription Description) CreateAsio(DeviceDescription description)
    {
        if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
        {
            throw new Exception("STA Thread required for ASIO creation.");
        }

        var device = new AsioOut(description.DeviceId);

        var driverExt = device.UnderlineDriver;
        if (description.ForceASIOBufferSize > 0)
        {
            var capability = driverExt.Capabilities;
            capability.BufferPreferredSize = description.ForceASIOBufferSize;

            _logger.LogDebug("Successfully forced ASIO buffer size to {BufferSize}", description.ForceASIOBufferSize);
        }

        //driverExt.Driver.GetBufferSize(out int minSize, out int maxSize, out int preferredSize, out int granularity);
        //var error = driverExt.Driver.GetLatencies(out int inputLatency, out var outputLatency);

        var latency = GetOutputLatency(driverExt);
        //var latency = GetRoundtripLatency(driverExt);

        return (device, description with { AsioLatencyMs = latency });
    }

    private double GetOutputLatency(AsioDriverExt driverExt)
    {
        double sampleRate = driverExt.Driver.GetSampleRate();
        int outputLatencySamples = driverExt.Capabilities.OutputLatency;
        double outputLatencyMs = (outputLatencySamples / sampleRate) * 1000.0;
        return outputLatencyMs;
    }

    private static double GetRoundtripLatency(AsioDriverExt driverExt)
    {
        var inputLatency = driverExt.Capabilities.InputLatency;
        var outputLatency = driverExt.Capabilities.OutputLatency;
        var preferredSize = driverExt.Capabilities.BufferPreferredSize;

        int userBufferComponent = preferredSize * 2;
        int totalRoundTrip = inputLatency + outputLatency;
        int hiddenOverhead = totalRoundTrip - userBufferComponent;

        // 计算毫秒数 (假设采样率为 44100，也可以通过 driver.GetSampleRate() 获取)
        double sampleRate = driverExt.Driver.GetSampleRate();
        double overheadMs = (hiddenOverhead / sampleRate) * 1000.0;
        double totalMs = (totalRoundTrip / sampleRate) * 1000.0;
        return totalMs;
    }

    private IEnumerable<DeviceDescription> EnumerateAllDevices()
    {
        foreach (var deviceDescription in EnumerateFromDirectSound()) yield return deviceDescription;
        foreach (var deviceDescription in EnumerateFromWasapi()) yield return deviceDescription;
        foreach (var deviceDescription in EnumerateFromAsio()) yield return deviceDescription;
    }

    private IEnumerable<DeviceDescription> EnumerateFromDirectSound()
    {
        IEnumerable<DirectSoundDeviceInfo> devices;
        try
        {
            devices = DirectSoundOut.Devices;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while enumerating DirectSoundOut device");
            devices = [];
        }

        foreach (var dev in devices)
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
                _logger.LogError(ex, "Error while enumerating DirectSoundOut device");
            }

            if (info != null)
            {
                yield return info;
            }
        }
    }

    private IEnumerable<DeviceDescription> EnumerateFromWasapi()
    {
        yield return DeviceDescription.WasapiDefault;

        IEnumerable<MMDevice> mmDeviceCollection;
        try
        {
            mmDeviceCollection = _mmDeviceEnumerator.EnumerateAudioEndPoints(DataFlow.All, DeviceState.All);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while enumerating WASAPI device");
            mmDeviceCollection = [];
        }

        foreach (var wasapi in mmDeviceCollection)
        {
            DeviceDescription? info = null;
            try
            {
                if (wasapi.DataFlow != DataFlow.Render || wasapi.State != DeviceState.Active) continue;
                info = new DeviceDescription
                {
                    DeviceId = wasapi.ID,
                    FriendlyName = wasapi.FriendlyName, // dynamic marshaling
                    WavePlayerType = WavePlayerType.WASAPI // dynamic marshaling
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while enumerating WASAPI device");
            }

            if (info != null)
            {
                yield return info;
            }
        }
    }

    private IEnumerable<DeviceDescription> EnumerateFromAsio()
    {
        string[] asioDriverNames;
        try
        {
            asioDriverNames = AsioOut.GetDriverNames();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while enumerating ASIO device");
            asioDriverNames = [];
        }

        foreach (var asio in asioDriverNames)
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
                _logger.LogError(ex, "Error while enumerating ASIO device");
            }

            if (info != null)
            {
                yield return info;
            }
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            try
            {
                _mmDeviceEnumerator.UnregisterEndpointNotificationCallback(_mmNotificationClient);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to unregister notification callback.");
            }

            _mmDeviceEnumerator.Dispose();
        }

        _disposed = true;
    }

    private class MmNotificationClient(AudioDeviceManager audioDeviceManager) : IMMNotificationClient
    {
        public void OnDeviceStateChanged(string deviceId, DeviceState newState) => audioDeviceManager.ClearCache();
        public void OnDeviceAdded(string pwstrDeviceId) => audioDeviceManager.ClearCache();
        public void OnDeviceRemoved(string deviceId) => audioDeviceManager.ClearCache();
        public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId) => audioDeviceManager.ClearCache();
        public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key) { }
    }
}