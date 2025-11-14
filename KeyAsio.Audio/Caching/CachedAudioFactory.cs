using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace KeyAsio.Audio.Caching;

public class CachedAudioFactory
{
    private static readonly byte[] EmptyWaveFile =
    [
        0x52, 0x49, 0x46, 0x46, 0x24, 0x00, 0x00, 0x00, 0x57, 0x41, 0x56, 0x45, 0x66, 0x6D, 0x74, 0x20,
        0x10, 0x00, 0x00, 0x00, 0x01, 0x00, 0x02, 0x00, 0x44, 0xAC, 0x00, 0x00, 0x10, 0xB1, 0x02, 0x00,
        0x04, 0x00, 0x10, 0x00, 0x64, 0x61, 0x74, 0x61, 0x00, 0x00, 0x00, 0x00
    ];

    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, CategoryCache> _categoryDictionary = new();

    public CachedAudioFactory(ILogger logger)
    {
        _logger = logger;
    }

    public async ValueTask<CachedAudio?> TryGetAsync(string fileIdentifier, string? category = null)
    {
        var categoryCache = _categoryDictionary.GetOrAdd(category ?? DefaultCategory, _ => new CategoryCache());
        var pathCache = categoryCache.PathHashCaches;
        var soundCache = categoryCache.AudioCachesByHash;
        if (pathCache.TryGetValue(fileIdentifier, out var hash) && soundCache.TryGetValue(hash, out var lazyTask))
        {
            return await lazyTask.Value;
        }

        return null;
    }

    public async Task<CacheResult> GetOrCreateOrEmpty(string fileIdentifier, Stream fileStream, WaveFormat waveFormat,
        string? category = null)
    {
        var cacheResult = await TryGetOrCreate(fileIdentifier, fileStream, waveFormat, category);
        if (cacheResult.Status != CacheGetStatus.Failed) return cacheResult;

        _logger?.LogWarning("Using default audio for {FileIdentifier} due to cache failure", fileIdentifier);

        var result = await GetOrCreateEmpty(fileIdentifier, waveFormat, category);
        return result with { Status = CacheGetStatus.Failed };
    }

    public async Task<CacheResult> GetOrCreateEmpty(string fileIdentifier, WaveFormat waveFormat,
        string? category = null)
    {
        using var fs = new MemoryStream(EmptyWaveFile);
        var cacheResult = await TryGetOrCreate(fileIdentifier, fs, waveFormat, category);
        return cacheResult.Status != CacheGetStatus.Failed
            ? cacheResult
            : throw new InvalidOperationException("Failed to create empty cached audio.");
    }

    public const string DefaultCategory = "default";
    public async Task<CacheResult> TryGetOrCreate(string fileIdentifier, Stream fileStream, WaveFormat waveFormat, string? category = null)
    {
        ArgumentNullException.ThrowIfNull(fileIdentifier, nameof(fileIdentifier));

        var categoryCache = _categoryDictionary.GetOrAdd(category ?? DefaultCategory, _ => new CategoryCache());
        var pathCache = categoryCache.PathHashCaches;
        var soundCache = categoryCache.AudioCachesByHash;

        if (pathCache.TryGetValue(fileIdentifier, out var hash) && soundCache.TryGetValue(hash, out var lazyTask))
        {
            try
            {
                var sound = await lazyTask.Value.ConfigureAwait(false);
                return new CacheResult(sound, CacheGetStatus.Hit);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error while awaiting cached sound: {Path}", fileIdentifier);
                return CacheResult.Failed;
            }
        }

        byte[] rentBuffer;
        int bytesRead;
        try
        {
            rentBuffer = ArrayPool<byte>.Shared.Rent((int)fileStream.Length);
            bytesRead = await fileStream.ReadAsync(rentBuffer.AsMemory(0, (int)fileStream.Length));
            var fileData = rentBuffer.AsMemory(0, bytesRead);
            hash = ComputeHash(fileData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read stream or compute hash for {FileIdentifier}", fileIdentifier);
            return CacheResult.Failed;
        }

        pathCache[fileIdentifier] = hash;

        var newLazyTask = new Lazy<Task<CachedAudio>>(() =>
        {
            try
            {
                return CreateAudioAsync(fileIdentifier, hash, rentBuffer, bytesRead, waveFormat);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rentBuffer);
            }
        });

        lazyTask = soundCache.GetOrAdd(hash, newLazyTask);

        try
        {
            var sound = await lazyTask.Value.ConfigureAwait(false);
            var created = ReferenceEquals(lazyTask, newLazyTask) && lazyTask.IsValueCreated;
            return new CacheResult(sound, created ? CacheGetStatus.Created : CacheGetStatus.Hit);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error while creating cached sound: {Path}", fileIdentifier);
            soundCache.TryRemove(hash, out _);
            return CacheResult.Failed;
        }
    }

    public void Clear(string? category = null)
    {
        if (!_categoryDictionary.TryGetValue(category ?? DefaultCategory, out var categoryCache)) return;
        categoryCache.PathHashCaches.Clear();
        categoryCache.AudioCachesByHash.Clear();
    }

    private async Task<CachedAudio> CreateAudioAsync(string fileIdentifier, string hash, byte[] rentBuffer, int bytesRead, WaveFormat waveFormat)
    {
        await using var smartWaveReader = new SmartWaveReader(rentBuffer, 0, bytesRead);
        var sampleChannel = GetResampledSampleChannel(smartWaveReader, fileIdentifier, waveFormat);

        var allSamples = new List<float>(sampleChannel.WaveFormat.SampleRate * sampleChannel.WaveFormat.Channels * 5);
        var readBuffer = ArrayPool<float>.Shared.Rent(sampleChannel.WaveFormat.SampleRate * sampleChannel.WaveFormat.Channels);
        try
        {
            var sw = Stopwatch.StartNew();
            int samplesRead;
            // 循环读取，直到 stream 结束
            while ((samplesRead = sampleChannel.Read(readBuffer, 0, readBuffer.Length)) > 0)
            {
                allSamples.AddRange(readBuffer.AsSpan(0, samplesRead));
            }

            _logger?.LogDebug("Cached {FileIdentifier} in {Elapsed:N2}ms", fileIdentifier, sw.Elapsed.TotalMilliseconds);
        }
        finally
        {
            ArrayPool<float>.Shared.Return(readBuffer);
        }

        return new CachedAudio(hash, allSamples.ToArray(), sampleChannel.WaveFormat);
    }

    private static string ComputeHash(ReadOnlyMemory<byte> data)
    {
        return Blake3.Hasher.Hash(data.Span).ToString();
    }

    private SampleChannel GetResampledSampleChannel(SmartWaveReader smartWaveReader, string fileIdentifier, WaveFormat waveFormat)
    {
        IWaveProvider streamToRead = smartWaveReader;
        var needsResampling = !CompareWaveFormat(smartWaveReader.WaveFormat, waveFormat);

        if (!needsResampling) return new SampleChannel(streamToRead, forceStereo: true);

        try
        {
            // MFResampler 会在 Dispose 时自动 Dispose 掉 smartWaveReader
            streamToRead = new MediaFoundationResampler(smartWaveReader, waveFormat)
            {
                ResamplerQuality = 60
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error while resampling audio file {File}", fileIdentifier);
            smartWaveReader.Dispose(); // 如果 MFResampler 构造失败，手动 Dispose
            throw;
        }

        // 用 SampleChannel 将 WaveStream (无论是原始的还是重采样的) 转换为 ISampleProvider
        return new SampleChannel(streamToRead, forceStereo: true);
    }

    private static bool CompareWaveFormat(WaveFormat format1, WaveFormat format2)
    {
        if (format2.Channels != format1.Channels) return false;
        if (format2.SampleRate != format1.SampleRate) return false;
        return true;
    }
}