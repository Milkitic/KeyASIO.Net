using System;
using System.IO;
using System.Threading.Tasks;
using NAudio.Wave;

namespace KeyAsio.Net.Audio;

internal static class WaveFormatFactory
{
    private static int _sampleRate = 44100;
    private static int _bits = 16;
    private static int _channels = 2;

    public readonly struct SampleQuality
    {
        public int Quality { get; }

        public SampleQuality(int quality)
        {
            Quality = quality;
        }

        public static implicit operator int(SampleQuality quality)
        {
            return quality.Quality;
        }

        public static implicit operator SampleQuality(int quality)
        {
            return new SampleQuality(quality);
        }

        public static int Highest => 60;
        public static int Lowest => 1;
    }

    public static int SampleRate
    {
        get => _sampleRate;
        set
        {
            if (Equals(_sampleRate, value)) return;
            _sampleRate = value;
            CachedSound.ClearCacheSounds();
            CachedSound.ClearDefaultCacheSounds();
        }
    }

    public static int Bits
    {
        get => _bits;
        set
        {
            if (Equals(_bits, value)) return;
            _bits = value;
            CachedSound.ClearCacheSounds();
            CachedSound.ClearDefaultCacheSounds();
        }
    }

    public static int Channels
    {
        get => _channels;
        set
        {
            if (Equals(_channels, value)) return;
            _channels = value;
            CachedSound.ClearCacheSounds();
            CachedSound.ClearDefaultCacheSounds();
        }
    }

    public static WaveFormat IeeeWaveFormat => WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, Channels);

    public static WaveFormat PcmWaveFormat => new WaveFormat(SampleRate, Bits, Channels);

    public static async Task<MyAudioFileReader> GetResampledAudioFileReader(string path, MyAudioFileReader.WaveStreamType type)
    {
        var stream = await Resample(path, type).ConfigureAwait(false);
        return stream is MyAudioFileReader afr ? afr : new MyAudioFileReader(stream, type);
    }

    private static async Task<Stream> Resample(string path, MyAudioFileReader.WaveStreamType type)
    {
        return await Task.Run(() =>
        {
            MyAudioFileReader? audioFileReader = null;
            try
            {
                Console.WriteLine($"Start reading {path}");
                audioFileReader = File.Exists(path)
                    ? new MyAudioFileReader(path)
                    : new MyAudioFileReader(SharedUtils.EmptyWaveFile, MyAudioFileReader.WaveStreamType.Wav);
                Console.WriteLine($"Finish reading {path}");
                if (CompareWaveFormat(audioFileReader.WaveFormat))
                {
                    return (Stream)audioFileReader;
                }

                Console.WriteLine($"Start resampling {path}");
                using (audioFileReader)
                {
                    if (type == MyAudioFileReader.WaveStreamType.Wav)
                    {
                        using var resampler = new MediaFoundationResampler(audioFileReader, PcmWaveFormat);
                        var stream = new MemoryStream();
                        resampler.ResamplerQuality = 60;
                        WaveFileWriter.WriteWavFileToStream(stream, resampler);
                        Console.WriteLine($"Resampled {path}");
                        stream.Position = 0;
                        return stream;
                    }
                    else
                    {
                        throw new NotSupportedException();
                    }
                }
            }
            catch (Exception ex)
            {
                audioFileReader?.Dispose();
                Console.Error.WriteLine($"Error while resampling audio file {path}: " + ex.Message);
                throw;
            }
        }).ConfigureAwait(false);
    }

    private static bool CompareWaveFormat(WaveFormat waveFormat)
    {
        var pcmWaveFormat = PcmWaveFormat;
        if (pcmWaveFormat.Channels != waveFormat.Channels) return false;
        if (pcmWaveFormat.SampleRate != waveFormat.SampleRate) return false;
        return true;
    }
}