using System;
using System.Collections.Generic;
using System.Linq;
using KeyAsio.Net.Models;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using NAudio.Wave;

namespace KeyAsio.Net
{
    public static class DeviceProvider
    {
        private static readonly MMDeviceEnumerator MMDeviceEnumerator;
        private static readonly MMNotificationClient MmNotificationClient;
        private static IWavePlayer _currentDevice;

        private class MMNotificationClient : IMMNotificationClient
        {
            public MMNotificationClient()
            {
                //_realEnumerator.RegisterEndpointNotificationCallback();
                //if (Environment.OSVersion.Version.Major < 6)
                //{
                //    throw new NotSupportedException("This functionality is only supported on Windows Vista or newer.");
                //}
            }

            public void OnDeviceStateChanged(string deviceId, DeviceState newState)
            {
                CacheList = null;
                //Console.WriteLine("OnDeviceStateChanged\n Device Id -->{0} : Device State {1}", deviceId, newState);
            }

            public void OnDeviceAdded(string pwstrDeviceId)
            {
                CacheList = null;
                //Console.WriteLine("OnDeviceAdded --> " + pwstrDeviceId);
            }

            public void OnDeviceRemoved(string deviceId)
            {
                CacheList = null;
                //Console.WriteLine("OnDeviceRemoved --> " + deviceId);
            }

            public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
            {
                CacheList = null;
                //Console.WriteLine("OnDefaultDeviceChanged --> {0}", flow.ToString());
            }

            public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key)
            {
                //fmtid & pid are changed to formatId and propertyId in the latest version NAudio
                //Console.WriteLine("OnPropertyValueChanged: formatId --> {0}  propertyId --> {1}", key.formatId.ToString(), key.propertyId.ToString());
            }
        }

        static DeviceProvider()
        {
            MMDeviceEnumerator = new MMDeviceEnumerator();
            if (Environment.OSVersion.Version.Major >= 6)
            {
                MmNotificationClient = new MMNotificationClient();
                MMDeviceEnumerator.RegisterEndpointNotificationCallback(MmNotificationClient);
            }
        }

        private static IReadOnlyList<DeviceMetadata> CacheList { get; set; }

        public static IWavePlayer GetCurrentDevice()
        {
            return _currentDevice;
        }

        public static IWavePlayer CreateDevice(out IDeviceInfo actualDeviceInfo, IDeviceInfo deviceInfo = null)
        {
            bool useDefault = false;
            if (deviceInfo is null)
            {
                deviceInfo = GetDefaultDeviceInfo();
                useDefault = true;
            }

            if (CacheList == null) GetAvailableDevices().ToList();

            IWavePlayer device = null;
            if (!useDefault && !CacheList.Contains(deviceInfo))
            {
                if (deviceInfo is WasapiInfo wasapiInfo)
                {
                    var foundResult = CacheList
                        .Where(k => k.WavePlayerType == WavePlayerType.WASAPI)
                        .Cast<WasapiInfo>()
                        .FirstOrDefault(k => k.DeviceId == wasapiInfo.DeviceId);
                    if (foundResult?.Device != null)
                    {
                        wasapiInfo.Device = foundResult.Device;
                    }
                    else
                    {
                        deviceInfo = GetDefaultDeviceInfo();
                    }
                }
                else
                {
                    deviceInfo = GetDefaultDeviceInfo();
                }
            }
            Execute.OnUiThread(() =>
            {
                try
                {
                    switch (deviceInfo.WavePlayerType)
                    {
                        case WavePlayerType.DirectSound:
                            var dsOut = (DirectSoundOutInfo)deviceInfo;
                            if (dsOut.Equals(DirectSoundOutInfo.Default))
                            {
                                device = new DirectSoundOut(40);
                            }
                            else
                            {
                                device = new DirectSoundOut(dsOut.DeviceGuid, Math.Max(latency, 40));
                            }
                            break;
                        case WavePlayerType.WASAPI:
                            var wasapi = (WasapiInfo)deviceInfo;
                            if (wasapi.Equals(WasapiInfo.Default))
                            {
                                device = new WasapiOut(AudioClientShareMode.Shared, latency);
                            }
                            else
                            {
                                device = new WasapiOut(wasapi.Device,
                                    isExclusive ? AudioClientShareMode.Exclusive : AudioClientShareMode.Shared, true, latency);
                            }
                            break;
                        case WavePlayerType.ASIO:
                            var asio = (AsioOutInfo)deviceInfo;
                            device = new AsioOut(asio.FriendlyName);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(deviceInfo.WavePlayerType),
                                deviceInfo.WavePlayerType, null);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error while creating device: " + ex.Message);
                    device?.Dispose();
                    deviceInfo = DirectSoundOutInfo.Default;
                    device = new DirectSoundOut(40);
                }
            });

            _currentDevice = device;
            actualDeviceInfo = deviceInfo;
            return device;
        }

        private static IDeviceInfo GetDefaultDeviceInfo()
        {
            IDeviceInfo deviceInfo;
            if (MMDeviceEnumerator.HasDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia))
            {
                deviceInfo = WasapiInfo.Default;
                Console.WriteLine("The output device in app's config was not detected in this system, use WASAPI default.");
            }
            else
            {
                deviceInfo = DirectSoundOutInfo.Default;
                Console.WriteLine("The output device in app's config was not detected " +
                                  "or no output device detected in this system, use DirectSoundOut default!!!");
            }

            return deviceInfo;
        }

        public static IReadOnlyList<DeviceMetadata> GetAvailableDevices()
        {
            if (CacheList != null)
            {
                return CacheList;
            }

            var all = YieldEnumerate().ToArray();
            CacheList = all;
            return all;
        }

        private static IEnumerable<DeviceMetadata> YieldEnumerate()
        {
            CacheList = new List<DeviceMetadata> { DeviceMetadata.WasapiDefault };
            yield return DeviceMetadata.WasapiDefault;

            foreach (var dev in DirectSoundOut.Devices)
            {
                //dev.Description
                DeviceMetadata info = null;
                try
                {
                    info = new DeviceMetadata
                    {
                        DeviceId = dev.Guid.ToString(), Latency = 40, WavePlayerType = WavePlayerType.DirectSound
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

            foreach (var wasapi in MMDeviceEnumerator.EnumerateAudioEndPoints(DataFlow.All, DeviceState.All))
            {
                //wasapi.FriendlyName
                DeviceMetadata info = null;
                try
                {
                    if (wasapi.DataFlow != DataFlow.Render || wasapi.State != DeviceState.Active) continue;
                    info = new DeviceMetadata(wasapi.FriendlyName, wasapi.ID)
                    {
                        Device = wasapi
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
                AsioOutInfo info = null;
                try
                {
                    info = new AsioOutInfo(asio);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error while enumerating ASIO device: {0}", ex.Message);
                }

                if (info != null)
                {
                    CacheList.Add(info);
                    yield return info;
                }
            }
        }
    }
}
