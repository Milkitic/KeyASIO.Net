using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace KeyAsio.Audio.Caching;

public class AudioCacheManager
{
    public const string DefaultCategory = "default";

    private static readonly byte[] EmptyWaveFile =
    [
        0x52, 0x49, 0x46, 0x46, 0x24, 0x00, 0x00, 0x00, 0x57, 0x41, 0x56, 0x45, 0x66, 0x6D, 0x74, 0x20,
        0x10, 0x00, 0x00, 0x00, 0x01, 0x00, 0x02, 0x00, 0x44, 0xAC, 0x00, 0x00, 0x10, 0xB1, 0x02, 0x00,
        0x04, 0x00, 0x10, 0x00, 0x64, 0x61, 0x74, 0x61, 0x00, 0x00, 0x00, 0x00
    ];

    private readonly ILogger<AudioCacheManager> _logger;
    private readonly ConcurrentDictionary<string, CategoryCache> _categoryDictionary = new();

    public AudioCacheManager(ILogger<AudioCacheManager> logger)
    {
        _logger = logger;
    }

    public async ValueTask<CachedAudio?> TryGetAsync(string cacheKey, string? category = null)
    {
        var categoryCache = _categoryDictionary.GetOrAdd(category ?? DefaultCategory, _ => new CategoryCache());
        var pathCache = categoryCache.PathHashCaches;
        var soundCache = categoryCache.AudioCachesByHash;
        if (pathCache.TryGetValue(cacheKey, out var hash) && soundCache.TryGetValue(hash, out var lazyTask))
        {
            return await lazyTask.Value;
        }

        return null;
    }

    public async Task<CacheResult> GetOrCreateOrEmptyFromFileAsync(string filePath, WaveFormat waveFormat,
        string? category = null)
    {
        var cacheKey = Path.GetFileName(filePath);
        if (!File.Exists(filePath))
        {
            return await GetOrCreateEmptyAsync(cacheKey, waveFormat, category);
        }

        await using var fs = File.OpenRead(filePath);
        return await GetOrCreateOrEmptyAsync(cacheKey, fs, waveFormat, category);
    }

    public async Task<CacheResult> GetOrCreateOrEmptyAsync(string cacheKey, Stream fileStream, WaveFormat waveFormat,
        string? category = null)
    {
        var cacheResult = await TryGetOrCreateAsync(cacheKey, fileStream, waveFormat, category);
        if (cacheResult.Status != CacheGetStatus.Failed) return cacheResult;

        _logger?.LogWarning("Using default audio for {CacheKey} due to cache failure", cacheKey);

        var result = await GetOrCreateEmptyAsync(cacheKey, waveFormat, category);
        return result with { Status = CacheGetStatus.Failed };
    }

    public async Task<CacheResult> GetOrCreateEmptyAsync(string cacheKey, WaveFormat waveFormat,
        string? category = null)
    {
        using var fs = new MemoryStream(EmptyWaveFile);
        var cacheResult = await TryGetOrCreateAsync(cacheKey, fs, waveFormat, category);
        return cacheResult.Status != CacheGetStatus.Failed
            ? cacheResult
            : throw new InvalidOperationException("Failed to create empty cached audio.");
    }

    public async Task<CacheResult> TryGetOrCreateAsync(string cacheKey, Stream fileStream, WaveFormat waveFormat,
        string? category = null)
    {
        ArgumentNullException.ThrowIfNull(cacheKey, nameof(cacheKey));

        var categoryCache = _categoryDictionary.GetOrAdd(category ?? DefaultCategory, _ => new CategoryCache());
        var pathCache = categoryCache.PathHashCaches;
        var soundCache = categoryCache.AudioCachesByHash;

        if (pathCache.TryGetValue(cacheKey, out var hash) && soundCache.TryGetValue(hash, out var lazyTask))
        {
            try
            {
                var sound = await lazyTask.Value.ConfigureAwait(false);
                return new CacheResult(sound, CacheGetStatus.Hit);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error while awaiting cached sound: {Path}", cacheKey);
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
            _logger.LogError(ex, "Failed to read stream or compute hash for {CacheKey}", cacheKey);
            return CacheResult.Failed;
        }

        pathCache[cacheKey] = hash;

        var newLazyTask = new Lazy<Task<CachedAudio>>(() =>
        {
            try
            {
                return CreateAudioAsync(cacheKey, hash, rentBuffer, bytesRead, waveFormat);
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
            _logger?.LogError(ex, "Error while creating cached sound: {Path}", cacheKey);
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

    private static string ComputeHash(ReadOnlyMemory<byte> data)
    {
        return Blake3.Hasher.Hash(data.Span).ToString();
    }

    private async Task<CachedAudio> CreateAudioAsync(string cacheKey, string hash, byte[] rentBuffer, int bytesRead,
        WaveFormat waveFormat)
    {
        await using var smartWaveReader = new SmartWaveReader(rentBuffer, 0, bytesRead);
        var (sampleChannel, totalFloatSamples) = GetResampledSampleChannel(smartWaveReader, cacheKey, waveFormat);

        var allSamples = new List<float>(totalFloatSamples == -1
            ? sampleChannel.WaveFormat.SampleRate * sampleChannel.WaveFormat.Channels * 5
            : (int)totalFloatSamples);
        var readBuffer =
            ArrayPool<float>.Shared.Rent(sampleChannel.WaveFormat.SampleRate * sampleChannel.WaveFormat.Channels);
        try
        {
            var sw = Stopwatch.StartNew();
            int samplesRead;
            // 循环读取，直到 stream 结束
            while ((samplesRead = sampleChannel.Read(readBuffer, 0, readBuffer.Length)) > 0)
            {
                allSamples.AddRange(readBuffer.AsSpan(0, samplesRead));
            }

            _logger?.LogDebug("Cached {CacheKey} in {Elapsed:N2}ms", cacheKey, sw.Elapsed.TotalMilliseconds);
        }
        finally
        {
            ArrayPool<float>.Shared.Return(readBuffer);
        }

        return new CachedAudio(hash, allSamples.ToArray(), sampleChannel.WaveFormat);
    }

    private (SampleChannel sampleChannel, long totalFloatSamples) GetResampledSampleChannel(
        SmartWaveReader smartWaveReader, string cacheKey, WaveFormat waveFormat)
    {
        IWaveProvider streamToRead = smartWaveReader;
        var needsResampling = !CompareWaveFormat(smartWaveReader.WaveFormat, waveFormat);

        long totalFloatSamples = -1; // 默认为未知长度

        if (!needsResampling)
        {
            // 长度已知：计算总浮点样本数
            var sourceStream = smartWaveReader.ReaderStream;
            var totalByteLength = sourceStream.Length;
            var bytesPerSample = sourceStream.WaveFormat.BitsPerSample / 8;
            var totalSourceSamples = (bytesPerSample > 0) ? totalByteLength / bytesPerSample : 0;

            totalFloatSamples = totalSourceSamples;

            // 我们在 CreateAudioAsync 总是 forceStereo: true
            if (sourceStream.WaveFormat.Channels == 1)
            {
                totalFloatSamples *= 2; // 单声道转立体声，样本数加倍
            }
        }
        else
        {
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
                _logger?.LogError(ex, "Error while resampling audio file {File}", cacheKey);
                smartWaveReader.Dispose(); // 如果 MFResampler 构造失败，手动 Dispose
                throw;
            }
        }

        // 用 SampleChannel 将 WaveStream (无论是原始的还是重采样的) 转换为 ISampleProvider
        return (new SampleChannel(streamToRead, forceStereo: true), totalFloatSamples);
    }

    private static bool CompareWaveFormat(WaveFormat format1, WaveFormat format2)
    {
        if (format2.Channels != format1.Channels) return false;
        if (format2.SampleRate != format1.SampleRate) return false;
        return true;
    }
}