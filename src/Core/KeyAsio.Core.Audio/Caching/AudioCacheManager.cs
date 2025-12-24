using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using KeyAsio.Core.Audio.Utils;
using KeyAsio.Core.Audio.Wave;
using Microsoft.Extensions.Logging;
using NAudio.Wave;

namespace KeyAsio.Core.Audio.Caching;

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

    public bool TryGet(string cacheKey, [NotNullWhen(true)] out CachedAudio? cachedAudio, string? category = null)
    {
        cachedAudio = null;

        if (!_categoryDictionary.TryGetValue(category ?? DefaultCategory, out var categoryCache))
            return false;
        if (!categoryCache.PathHashCaches.TryGetValue(cacheKey, out var hash))
            return false;
        if (!categoryCache.AudioCachesByHash.TryGetValue(hash, out var lazyTask))
            return false;
        if (!lazyTask.IsValueCreated)
            return false;

        var task = lazyTask.Value;

        if (task.IsCompletedSuccessfully)
        {
            cachedAudio = task.Result;
            return true;
        }

        return false;
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
        if (!File.Exists(filePath))
            return await GetOrCreateEmptyAsync(filePath, waveFormat, category);

        var exist = await TryGetAsync(filePath, category);
        if (exist != null) return new CacheResult(exist, CacheGetStatus.Hit);

        await using var fs = File.OpenRead(filePath);
        return await GetOrCreateOrEmptyAsync(filePath, fs, waveFormat, category);
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
        var exist = await TryGetAsync(cacheKey, category);
        if (exist != null) return new CacheResult(exist, CacheGetStatus.Hit);

        using var fs = new MemoryStream(EmptyWaveFile);
        var cacheResult = await TryGetOrCreateAsync(cacheKey, fs, waveFormat, category);
        return cacheResult.Status != CacheGetStatus.Failed
            ? cacheResult
            : throw new InvalidOperationException("Failed to create empty cached audio.");
    }

    public CachedAudio CreateDynamic(string key, WaveFormat waveFormat)
    {
        var owner = UnmanagedByteMemoryOwner.Allocate(0);
        var cachedAudio = new CachedAudio(key, owner, 0, new WaveFormat(waveFormat.SampleRate, waveFormat.Channels));
        return cachedAudio;
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
                return CreateAudioCacheAsync(cacheKey, hash, rentBuffer, bytesRead, waveFormat);
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

    public void ClearAll()
    {
        foreach (var category in _categoryDictionary.Keys)
        {
            Clear(category);
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

    private async Task<CachedAudio> CreateAudioCacheAsync(string cacheKey, string hash, byte[] rentBuffer,
        int bytesRead, WaveFormat waveFormat)
    {
        await using var audioFileReader = new AudioFileReader(rentBuffer, 0, bytesRead);
        var (waveProvider, estimatedShortSamples) =
            GetWaveProvider(audioFileReader, cacheKey, waveFormat.SampleRate);

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

    private (IWaveProvider waveProvider, long estimatedShortSamples) GetWaveProvider(
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
}