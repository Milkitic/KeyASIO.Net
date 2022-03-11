using NAudio.Wave;

namespace KeyAsio.Net.Audio;

internal static class ResamplingHelper
{
    public static async Task<MyAudioFileReader> GetResampledAudioFileReader(string path, WaveType type, WaveFormat newWaveFormat)
    {
        var stream = await Resample(path, type, newWaveFormat).ConfigureAwait(false);
        return stream is MyAudioFileReader afr ? afr : new MyAudioFileReader(stream, type);
    }

    private static async Task<Stream> Resample(string path, WaveType type, WaveFormat newWaveFormat)
    {
        return await Task.Run(() =>
        {
            MyAudioFileReader? audioFileReader = null;
            try
            {
                Console.WriteLine($"Start reading {path}");
                audioFileReader = File.Exists(path)
                    ? new MyAudioFileReader(path)
                    : new MyAudioFileReader(SharedUtils.EmptyWaveFile, WaveType.Wav);
                Console.WriteLine($"Finish reading {path}");
                if (CompareWaveFormat(audioFileReader.WaveFormat, newWaveFormat))
                {
                    return (Stream)audioFileReader;
                }

                Console.WriteLine($"Start resampling {path}");
                using (audioFileReader)
                {
                    if (type == WaveType.Wav)
                    {
                        using var resampler = new MediaFoundationResampler(audioFileReader, newWaveFormat);
                        var stream = new MemoryStream();
                        resampler.ResamplerQuality = 60; // highest
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

    private static bool CompareWaveFormat(WaveFormat format1, WaveFormat format2)
    {
        if (format2.Channels != format1.Channels) return false;
        if (format2.SampleRate != format1.SampleRate) return false;
        return true;
    }
}