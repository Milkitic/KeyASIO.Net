using System;
using System.IO;
using System.Threading.Tasks;
using KeyAsio.Net.Audio;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using Newtonsoft.Json;

namespace KeyAsio.Net
{
    public class WasapiInfo : IDeviceInfo
    {
        public WasapiInfo()
        {

        }

        public WasapiInfo(string friendlyName, string device)
        {
            FriendlyName = friendlyName;
            DeviceId = device;
            Device = null;
        }

        public OutputMethod OutputMethod => OutputMethod.Wasapi;
        [JsonProperty]
        public string FriendlyName { get; private set; }
        [JsonProperty]
        public string DeviceId { get; private set; }

        [JsonIgnore]
        public MMDevice Device { get; set; }

        public static WasapiInfo Default { get; } = new WasapiInfo(null, null);

        public override bool Equals(object obj)
        {
            if (obj is WasapiInfo deviceInfo)
                return Equals(deviceInfo);
            return false;
        }

        protected bool Equals(WasapiInfo other)
        {
            return FriendlyName == other.FriendlyName && DeviceId == other.DeviceId && Equals(Device, other.Device);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (FriendlyName != null ? FriendlyName.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (DeviceId != null ? DeviceId.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Device != null ? Device.GetHashCode() : 0);
                return hashCode;
            }
        }
    }

    internal static class WaveFormatFactory
    {
        public struct ResamplerQuality
        {
            public int Quality { get; }

            public ResamplerQuality(int quality)
            {
                Quality = quality;
            }

            public static implicit operator int(ResamplerQuality quality)
            {
                return quality.Quality;
            }

            public static implicit operator ResamplerQuality(int quality)
            {
                return new ResamplerQuality(quality);
            }

            public static int Highest => 60;
            public static int Lowest => 1;
        }

        public static WaveFormat IeeeWaveFormat =>
            WaveFormat.CreateIeeeFloatWaveFormat(AppSettings.Default.SampleRate, AppSettings.Default.ChannelCount);

        public static WaveFormat PcmWaveFormat => new WaveFormat(AppSettings.Default.SampleRate,
            AppSettings.Default.Bits, AppSettings.Default.ChannelCount);

        private static Stream Resample(string path, string targetPath)
        {
            try
            {
                var fileReader = new MyAudioFileReader(path);
                if (CompareWaveFormat(fileReader.WaveFormat))
                {
                    return fileReader;
                }

                using (fileReader)
                using (var resampler = new MediaFoundationResampler(fileReader, PcmWaveFormat))
                using (var stream = new FileStream(targetPath, FileMode.Create))
                {
                    resampler.ResamplerQuality = ResamplerQuality.Highest;
                    WaveFileWriter.WriteWavFileToStream(stream, resampler);
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error while resampling audio file: {path}\r\n" + ex.Message);
                throw;
            }
        }

        private static Stream Resample(string path)
        {
            MyAudioFileReader fileReader = null;
            try
            {
                Console.WriteLine("Start reading {0}", path);
                fileReader = new MyAudioFileReader(path);
                Console.WriteLine("Finish reading {0}", path);
                if (CompareWaveFormat(fileReader.WaveFormat))
                {
                    return (Stream)fileReader;
                }

                Console.WriteLine("Start resampling {0}", path);
                using (fileReader)
                {
                    using (var resampler = new MediaFoundationResampler(fileReader, PcmWaveFormat))
                    {
                        var stream = new MemoryStream();
                        resampler.ResamplerQuality = 60;
                        WaveFileWriter.WriteWavFileToStream(stream, resampler);
                        Console.WriteLine("Resampled {0}", path);
                        stream.Position = 0;
                        return stream;
                    }
                }
            }
            catch (Exception ex)
            {
                fileReader?.Dispose();
                Console.Error.WriteLine($"Error while resampling audio file: {path}\r\n" + ex.Message);
                throw;
            }
        }

        private static bool CompareWaveFormat(WaveFormat waveFormat)
        {
            var pcmWaveFormat = PcmWaveFormat;
            if (pcmWaveFormat.Channels != waveFormat.Channels) return false;
            if (pcmWaveFormat.SampleRate != waveFormat.SampleRate) return false;
            return true;
        }

        public static MyAudioFileReader GetResampledFileReader(string filePath)
        {
            var stream = Resample(filePath);
            return stream is MyAudioFileReader afr ? afr : new MyAudioFileReader(stream, MyAudioFileReader.WaveStreamType.Wav);
        }
    }
}