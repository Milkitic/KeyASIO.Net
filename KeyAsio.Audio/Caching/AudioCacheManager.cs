using System.Buffers;
using System.Collections.Concurrent;
using KeyAsio.Audio.Utils;
using KeyAsio.Audio.Wave;
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
    private readonly ConcurrentDictionary<int, WaveFormat> _waveFormats = new();

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
                return CreatePcmCacheAsync(cacheKey, hash, rentBuffer, bytesRead, waveFormat);
                //return CreateAudioAsync(cacheKey, hash, rentBuffer, bytesRead, waveFormat);
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

        foreach (var lazy in categoryCache.AudioCachesByHash.Values)
        {
            if (!lazy.IsValueCreated) continue;
            var task = lazy.Value;

            if (task.IsCompletedSuccessfully)
            {
                task.Result.Dispose();
                continue;
            }

            _ = task.ContinueWith(t =>
            {
                if (t.IsCompletedSuccessfully)
                {
                    t.Result.Dispose();
                }
            }, TaskScheduler.Default);
        }

        categoryCache.PathHashCaches.Clear();
        categoryCache.AudioCachesByHash.Clear();
    }

    private static string ComputeHash(ReadOnlyMemory<byte> data)
    {
        return Blake3.Hasher.Hash(data.Span).ToString();
    }

    private async Task<CachedIeeeAudio> CreateIeeeCacheAsync(string cacheKey, string hash, byte[] rentBuffer,
        int bytesRead, WaveFormat waveFormat)
    {
        await using var audioFileReader = new AudioFileReader(rentBuffer, 0, bytesRead);
        var (sampleChannel, totalFloatSamples) = GetResampledSampleChannel(audioFileReader, cacheKey, waveFormat);

        var allSamples = new List<float>(totalFloatSamples == -1
            ? sampleChannel.WaveFormat.SampleRate * sampleChannel.WaveFormat.Channels * 5
            : (int)totalFloatSamples);
        var readBuffer =
            ArrayPool<float>.Shared.Rent(sampleChannel.WaveFormat.SampleRate * sampleChannel.WaveFormat.Channels);
        try
        {
            var sw = HighPrecisionTimer.StartNew();
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

        return new CachedIeeeAudio(hash, allSamples.ToArray(), sampleChannel.WaveFormat);
    }

    private async Task<CachedAudio> CreatePcmCacheAsync(string cacheKey, string hash, byte[] rentBuffer,
        int bytesRead, WaveFormat waveFormat)
    {
        await using var audioFileReader = new AudioFileReader(rentBuffer, 0, bytesRead);
        var (waveProvider, estimatedShortSamples) =
            GetPcmWaveProvider(audioFileReader, cacheKey, waveFormat.SampleRate);

        var sw = HighPrecisionTimer.StartNew();

        int estimatedBytes = Math.Max(estimatedShortSamples > 0
            ? (int)(estimatedShortSamples * 2) // 16-bit = 2 bytes
            : (int)(audioFileReader.Length), 4096);

        var owner = UnmanagedByteMemoryOwner.Allocate(estimatedBytes);

        int totalBytes = 0;
        int currentCapacity = estimatedBytes;

        var bufferSize = Math.Min(waveProvider.WaveFormat.AverageBytesPerSecond, 64 * 1024);
        var readBuffer = ArrayPool<byte>.Shared.Rent(bufferSize);

        try
        {
            int read;
            while ((read = waveProvider.Read(readBuffer, 0, readBuffer.Length)) > 0)
            {
                // 检查容量是否足够
                if (totalBytes + read > currentCapacity)
                {
                    int newCapacity = Math.Max(currentCapacity * 2, totalBytes + read);
                    owner.Resize(newCapacity);
                    currentCapacity = newCapacity;
                }

                new Span<byte>(readBuffer, 0, read).CopyTo(owner.Memory.Span.Slice(totalBytes));
                totalBytes += read;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(readBuffer);
            if (waveProvider is IDisposable d) d.Dispose();
        }

        if (totalBytes < currentCapacity)
        {
            owner.Resize(totalBytes);
        }

        _logger?.LogDebug("Cached {CacheKey} (Unmanaged) in {Elapsed:N2}ms", cacheKey, sw.Elapsed.TotalMilliseconds);
        return new CachedAudio(hash, owner, totalBytes, waveProvider.WaveFormat);
    }

    private (SampleChannel sampleChannel, long totalFloatSamples) GetResampledSampleChannel(
        AudioFileReader audioFileReader, string cacheKey, WaveFormat waveFormat)
    {
        IWaveProvider streamToRead = audioFileReader;
        var needsResampling = !CompareWaveFormat(audioFileReader.WaveFormat, waveFormat);

        long totalFloatSamples = -1; // 默认为未知长度

        if (!needsResampling)
        {
            // 长度已知，计算总浮点样本数
            var sourceStream = audioFileReader.ReaderStream;
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
                // MFResampler 会在 Dispose 时自动 Dispose 掉 audioFileReader
                streamToRead = new MediaFoundationResampler(audioFileReader, waveFormat)
                {
                    ResamplerQuality = 60
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error while resampling audio file {File}", cacheKey);
                audioFileReader.Dispose(); // 如果 MFResampler 构造失败，手动 Dispose
                throw;
            }
        }

        // 用 SampleChannel 将 WaveStream (无论是原始的还是重采样的) 转换为 ISampleProvider
        return (new SampleChannel(streamToRead, forceStereo: true), totalFloatSamples);
    }

    private (IWaveProvider waveProvider, long estimatedShortSamples) GetPcmWaveProvider(
        AudioFileReader audioFileReader, string cacheKey, int targetSampleRate)
    {
        var targetFormat = GetPcm16WaveFormat(targetSampleRate); // 强制 16-bit PCM，双声道，目标采样率

        IWaveProvider provider;
        long estimatedSamples = -1;

        bool formatMatches = audioFileReader.WaveFormat.SampleRate == targetSampleRate &&
                             audioFileReader.WaveFormat is
                             {
                                 Channels: 2,
                                 Encoding: WaveFormatEncoding.Pcm,
                                 BitsPerSample: 16
                             };

        if (formatMatches)
        {
            provider = audioFileReader;
            if (audioFileReader.Length > 0)
            {
                estimatedSamples = audioFileReader.Length / 2;
            }
        }
        else
        {
            try
            {
                var resampler = new MediaFoundationResampler(audioFileReader, targetFormat)
                {
                    ResamplerQuality = 60 // Best quality
                };
                provider = resampler;
                if (audioFileReader.Length > 0)
                {
                    double ratio = (double)targetFormat.AverageBytesPerSecond /
                                   audioFileReader.WaveFormat.AverageBytesPerSecond;
                    estimatedSamples = (long)(audioFileReader.Length * ratio / 2);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error initializing resampler for {File}", cacheKey);
                audioFileReader.Dispose();
                throw;
            }
        }

        return (provider, estimatedSamples);
    }

    private WaveFormat GetPcm16WaveFormat(int targetSampleRate)
    {
        return _waveFormats.GetOrAdd(targetSampleRate, rate => new WaveFormat(rate, 16, 2));
    }

    private static bool CompareWaveFormat(WaveFormat format1, WaveFormat format2)
    {
        if (format2.Channels != format1.Channels) return false;
        if (format2.SampleRate != format1.SampleRate) return false;
        return true;
    }
}