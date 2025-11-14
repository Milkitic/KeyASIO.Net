using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using NAudio.Wave;
using NAudio.Wave.Asio;

namespace KeyAsio.Audio;

[SupportedOSPlatform("windows6.0")]
public class DeviceCreationHelper : IDisposable
{
    private static readonly FieldInfo? AsioExtField;
    private static readonly FieldInfo? CapabilityField;

    private readonly ILogger<DeviceCreationHelper> _logger;
    private readonly MMDeviceEnumerator _mmDeviceEnumerator;
    private readonly MmNotificationClient _mmNotificationClientCallBack;

    private bool _disposed;
    private Lazy<Task<IReadOnlyList<DeviceDescription>>> _cachedDevices;

    static DeviceCreationHelper()
    {
        // HACK: Accessing private NAudio fields to force ASIO buffer size.
        // This targets NAudio v2.2.1.
        // AsioOut.driver -> AsioDriverExt.capability -> AsioDriverCapability.BufferPreferredSize
        AsioExtField = typeof(AsioOut).GetField("driver", BindingFlags.Instance | BindingFlags.NonPublic);
        CapabilityField = typeof(AsioDriverExt).GetField("capability", BindingFlags.Instance | BindingFlags.NonPublic);
    }

    public DeviceCreationHelper(ILogger<DeviceCreationHelper> logger)
    {
        _logger = logger;
        if (Environment.OSVersion.Version.Major < 6)
        {
            throw new NotSupportedException("This functionality is only supported on Windows Vista or newer.");
        }

        _mmDeviceEnumerator = new MMDeviceEnumerator();

        _cachedDevices = CreateLazyDeviceListAsync();

        _mmNotificationClientCallBack = new MmNotificationClient(this);
        _mmDeviceEnumerator.RegisterEndpointNotificationCallback(_mmNotificationClientCallBack);
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
            () => Task.Run(IReadOnlyList<DeviceDescription> () => EnumerateConfigurations().ToArray()),
            LazyThreadSafetyMode.ExecutionAndPublication
        );
    }

    private (IWavePlayer Player, DeviceDescription ActualDescription) CreationCore(DeviceDescription? description)
    {
        description ??= _mmDeviceEnumerator.HasDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia)
            ? DeviceDescription.WasapiDefault
            : DeviceDescription.DirectSoundDefault;
        IWavePlayer device = description.WavePlayerType switch
        {
            WavePlayerType.DirectSound => CreateDirectSound(description),
            WavePlayerType.WASAPI => CreateWasapi(description),
            WavePlayerType.ASIO => CreateAsio(description),
            _ => throw new ArgumentOutOfRangeException()
        };
        return (device, description);
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

    private AsioOut CreateAsio(DeviceDescription description)
    {
        if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
        {
            throw new Exception("STA Thread required for ASIO creation.");
        }

        var device = new AsioOut(description.DeviceId);

        if (description.ForceASIOBufferSize <= 0) return device;

        if (AsioExtField == null || CapabilityField == null)
        {
            _logger.LogWarning(
                "Failed to force ASIO buffer size: Reflection fields not found. (NAudio internal API may have changed).");
            return device;
        }

        var driver = AsioExtField.GetValue(device);
        if (driver == null)
        {
            _logger.LogWarning(
                "Failed to force ASIO buffer size: Could not get 'driver' instance. (NAudio internal API may have changed).");
            return device;
        }

        var capability = (AsioDriverCapability?)CapabilityField.GetValue(driver);
        if (capability == null)
        {
            _logger.LogWarning(
                "Failed to force ASIO buffer size: Could not get 'capability' instance. (NAudio internal API may have changed).");
            return device;
        }

        capability.BufferPreferredSize = description.ForceASIOBufferSize;
        _logger.LogDebug("Successfully forced ASIO buffer size to {BufferSize}", description.ForceASIOBufferSize);
        return device;
    }

    private IEnumerable<DeviceDescription> EnumerateConfigurations()
    {
        foreach (var deviceDescription in EnmumerateFromDirectSound()) yield return deviceDescription;
        foreach (var deviceDescription in EnumerateFromWasapi()) yield return deviceDescription;
        foreach (var deviceDescription in EnumerateFromAsio()) yield return deviceDescription;
    }

    private IEnumerable<DeviceDescription> EnmumerateFromDirectSound()
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

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            try
            {
                _mmDeviceEnumerator.UnregisterEndpointNotificationCallback(_mmNotificationClientCallBack);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to unregister notification callback.");
            }

            _mmDeviceEnumerator.Dispose();
        }

        _disposed = true;
    }

    private class MmNotificationClient(DeviceCreationHelper deviceCreationHelper) : IMMNotificationClient
    {
        public void OnDeviceStateChanged(string deviceId, DeviceState newState) => deviceCreationHelper.ClearCache();
        public void OnDeviceAdded(string pwstrDeviceId) => deviceCreationHelper.ClearCache();
        public void OnDeviceRemoved(string deviceId) => deviceCreationHelper.ClearCache();
        public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId) => deviceCreationHelper.ClearCache();
        public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key) { }
    }
}