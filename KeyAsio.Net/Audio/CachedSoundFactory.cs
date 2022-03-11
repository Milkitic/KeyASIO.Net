using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualBasic;
using NAudio.Wave;

namespace KeyAsio.Net.Audio;

public static class CachedSoundFactory
{
    public static WaveType WavType => WaveType.Wav;
    private static readonly ConcurrentDictionary<string, CachedSound?> CachedDictionary = new();

    public static IReadOnlyDictionary<string, CachedSound?> CachedSounds { get; } =
        new ReadOnlyDictionary<string, CachedSound?>(CachedDictionary);
    public static bool ContainsCache(string? path)
    {
        if (path == null) return false;
        return CachedSounds.ContainsKey(path);
    }

    public static async Task<CachedSound?> GetOrCreateCacheSound(WaveFormat waveFormat, string? path)
    {
        if (path == null) return null;
        if (CachedDictionary.TryGetValue(path, out var value))
        {
            return value;
        }

        if (!File.Exists(path))
        {
            CachedDictionary.TryAdd(path, null);
            return null;
        }

        CachedSound cachedSound;
        try
        {
            cachedSound = await CreateCacheFromFile(waveFormat, path).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while creating cached sound: {path}" + ex.Message);
            CachedDictionary.TryAdd(path, null);
            return null;
        }

        // Cache each file once before play.
        var sound = CachedDictionary.GetOrAdd(path, cachedSound);

        Console.WriteLine("Total size of cache usage: {0}", SharedUtils.SizeSuffix(
            CachedDictionary.Values.Sum(k => k?.AudioData.Length * sizeof(float) ?? 0)));

        return sound;
    }

    public static void ClearCacheSounds()
    {
        CachedDictionary.Clear();
    }

    private static async Task<CachedSound> CreateCacheFromFile(WaveFormat waveFormat, string filePath)
    {
        await using var audioFileReader =
            await ResamplingHelper.GetResampledAudioFileReader(filePath, WavType, waveFormat).ConfigureAwait(false);
        var sw = Stopwatch.StartNew();
        var wholeData = new float[(int)(audioFileReader.Length / 4)];
        var actualWaveFormat = audioFileReader.WaveFormat;

        var length = actualWaveFormat.SampleRate * actualWaveFormat.Channels;
        var readBuffer = ArrayPool<float>.Shared.Rent(length);
        try
        {
            int samplesRead;
            int offset = 0;
            while ((samplesRead = audioFileReader.Read(readBuffer, 0, length)) > 0)
            {
                readBuffer.AsSpan(0, samplesRead).CopyTo(wholeData.AsSpan(offset, samplesRead));
                offset += samplesRead;
            }
                
            return new CachedSound(filePath, wholeData, audioFileReader.TotalTime, actualWaveFormat);
        }
        finally
        {
            ArrayPool<float>.Shared.Return(readBuffer);
            Console.WriteLine($"Cached {Path.GetFileName(filePath)} in {sw.Elapsed.TotalMilliseconds:N2}ms");
        }
    }
}