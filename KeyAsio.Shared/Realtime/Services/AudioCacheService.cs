using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Coosu.Beatmap.Extensions;
using Coosu.Beatmap.Extensions.Playback;
using KeyAsio.Audio;
using KeyAsio.Audio.Caching;
using KeyAsio.MemoryReading.Logging;
using KeyAsio.Shared.Models;
using KeyAsio.Shared.Utils;
using NAudio.Wave;

namespace KeyAsio.Shared.Realtime.Services;

public class AudioCacheService
{
    private static readonly ILogger Logger = LogUtils.GetLogger(nameof(AudioCacheService));
    private static readonly string[] SkinAudioFiles = ["combobreak"];
    private static readonly ParallelOptions ParallelOptions = new()
    {
        MaxDegreeOfParallelism = 1, // Preserve use
    };

    private readonly HitsoundFileCache _hitsoundFileCache = new();
    private readonly ConcurrentDictionary<HitsoundNode, CachedAudio> _playNodeToCachedSoundMapping = new();
    private readonly ConcurrentDictionary<string, CachedAudio> _filenameToCachedSoundMapping = new();

    private readonly IServiceProvider _serviceProvider;
    private readonly AudioEngine _audioEngine;
    private readonly AudioCacheManager _audioCacheManager;
    private readonly SharedViewModel _sharedViewModel;
    private string? _beatmapFolder;
    private string? _audioFilename;

    public AudioCacheService(IServiceProvider serviceProvider, AudioEngine audioEngine, AudioCacheManager audioCacheManager, SharedViewModel sharedViewModel)
    {
        _serviceProvider = serviceProvider;
        _audioEngine = audioEngine;
        _audioCacheManager = audioCacheManager;
        _sharedViewModel = sharedViewModel;
    }

    public void SetContext(string? beatmapFolder, string? audioFilename)
    {
        _beatmapFolder = beatmapFolder;
        _audioFilename = audioFilename;
    }

    public void ClearCaches()
    {
        _audioCacheManager.Clear();
        _playNodeToCachedSoundMapping.Clear();
        _filenameToCachedSoundMapping.Clear();
    }

    public bool TryGetAudioByNode(HitsoundNode node, out CachedAudio cachedSound)
    {
        if (!_playNodeToCachedSoundMapping.TryGetValue(node, out cachedSound)) return false;
        return node is PlayableNode;
    }

    public bool TryGetCachedSound(string filenameWithoutExt, out CachedAudio? cachedSound)
    {
        return _filenameToCachedSoundMapping.TryGetValue(filenameWithoutExt, out cachedSound);
    }

    public void PrecacheMusicAndSkinInBackground()
    {
        if (_beatmapFolder == null)
        {
            Logger.Warn("Beatmap folder is null, stop adding cache.");
            return;
        }

        if (_audioEngine.CurrentDevice == null)
        {
            Logger.Warn("AudioEngine is null, stop adding cache.");
            return;
        }

        var folder = _beatmapFolder;
        var waveFormat = _audioEngine.EngineWaveFormat;
        var skinFolder = _sharedViewModel.SelectedSkin?.Folder ?? "";

        Task.Run(async () =>
        {
            if (folder != null && _audioFilename != null)
            {
                var musicPath = Path.Combine(folder, _audioFilename);

                CacheGetStatus status;
                await using (var fs = File.OpenRead(musicPath))
                {
                    (_, status) = await _audioCacheManager.GetOrCreateOrEmptyAsync(musicPath, fs, waveFormat);
                }

                if (status == CacheGetStatus.Failed)
                {
                    Logger.Warn("Caching music failed: " + (File.Exists(musicPath) ? musicPath : "FileNotFound"));
                }
                else if (status == CacheGetStatus.Hit)
                {
                    Logger.Info("Got music cache: " + musicPath);
                }
                else if (status == CacheGetStatus.Created)
                {
                    Logger.Info("Cached music: " + musicPath);
                }
            }

            try
            {
                await Parallel.ForEachAsync(SkinAudioFiles, ParallelOptions, async (skinAudioFile, _) =>
                {
                    await AddSkinCacheAsync(skinAudioFile, folder!, skinFolder, waveFormat);
                });
            }
            catch (OperationCanceledException)
            {
                Logger.Warn("Hitsound caching was cancelled.");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "An error occurred during parallel hitsound caching.");
            }
        });
    }

    public void PrecacheHitsoundsRangeInBackground(
        int startTime,
        int endTime,
        IEnumerable<HitsoundNode> playableNodes,
        [CallerArgumentExpression("playableNodes")] string? expression = null)
    {
        if (_beatmapFolder == null)
        {
            Logger.Warn("Beatmap folder is null, stop adding cache.");
            return;
        }

        if (_audioEngine.CurrentDevice == null)
        {
            Logger.Warn("AudioEngine is null, stop adding cache.");
            return;
        }

        if (playableNodes is System.Collections.IList { Count: 0 })
        {
            Logger.Warn($"{expression} has no hitsounds, stop adding cache.");
            return;
        }

        var folder = _beatmapFolder;
        var waveFormat = _audioEngine.EngineWaveFormat;
        var skinFolder = _sharedViewModel.SelectedSkin?.Folder ?? "";

        Task.Run(async () =>
        {
            using var _ = DebugUtils.CreateTimer($"CacheAudio {startTime}~{endTime}", Logger);
            var nodesToCache = playableNodes.Where(k => k.Offset >= startTime && k.Offset < endTime);

            try
            {
                await Parallel.ForEachAsync(nodesToCache, ParallelOptions, async (node, _) =>
                {
                    await AddHitsoundCacheAsync(node, folder!, skinFolder, waveFormat);
                });
            }
            catch (OperationCanceledException)
            {
                Logger.Warn("Hitsound caching was cancelled.");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "An error occurred during parallel hitsound caching.");
            }
        });
    }

    public async Task<CachedAudio?> AddSkinCacheAsync(
        string filenameWithoutExt,
        string beatmapFolder,
        string skinFolder,
        WaveFormat waveFormat)
    {
        if (_filenameToCachedSoundMapping.TryGetValue(filenameWithoutExt, out var value)) return value;

        var category = "default";
        var filename = _hitsoundFileCache.GetFileUntilFind(beatmapFolder, filenameWithoutExt, out var useUserSkin);
        string path;
        if (useUserSkin)
        {
            category = "internal";
            filename = _hitsoundFileCache.GetFileUntilFind(skinFolder, filenameWithoutExt, out useUserSkin);
            path = useUserSkin
                ? Path.Combine(_sharedViewModel.DefaultFolder, $"{filenameWithoutExt}.ogg")
                : Path.Combine(skinFolder, filename);
        }
        else
        {
            path = Path.Combine(beatmapFolder, filename);
        }

        CachedAudio result;
        CacheGetStatus status;
        await using (var fs = File.OpenRead(path))
        {
            (result!, status) = await _audioCacheManager.GetOrCreateOrEmptyAsync(path, fs, waveFormat, category);
        }

        if (status == CacheGetStatus.Failed)
        {
            Logger.Warn("Caching effect failed: " + (File.Exists(path) ? path : "FileNotFound"));
        }
        else if (status == CacheGetStatus.Hit)
        {
            Logger.Info("Got effect cache: " + path);
        }
        else if (status == CacheGetStatus.Created)
        {
            Logger.Info("Cached effect: " + path);
        }

        _filenameToCachedSoundMapping.TryAdd(filenameWithoutExt, result!);

        return result;
    }

    public async Task AddHitsoundCacheAsync(
        HitsoundNode hitsoundNode,
        string beatmapFolder,
        string skinFolder,
        WaveFormat waveFormat)
    {
        var manager = (RealtimeModeManager)_serviceProvider.GetService(typeof(RealtimeModeManager))!;
        if (!manager.IsStarted)
        {
            Logger.Warn("Isn't started, stop adding cache.");
            return;
        }

        if (hitsoundNode.Filename == null)
        {
            if (hitsoundNode is PlayableNode)
            {
                Logger.Warn("Filename is null, add null cache.");
            }

            var cacheResult = await _audioCacheManager.GetOrCreateEmptyAsync("null", waveFormat);
            _playNodeToCachedSoundMapping.TryAdd(hitsoundNode, cacheResult.CachedAudio!);
            return;
        }

        var path = Path.Combine(beatmapFolder, hitsoundNode.Filename);
        var category = "default";
        if (hitsoundNode.UseUserSkin)
        {
            category = "internal";
            var filename = _hitsoundFileCache.GetFileUntilFind(skinFolder, hitsoundNode.Filename, out var useUserSkin);
            path = useUserSkin
                ? Path.Combine(_sharedViewModel.DefaultFolder, $"{hitsoundNode.Filename}.ogg")
                : Path.Combine(skinFolder, filename);
        }

        CachedAudio result;
        CacheGetStatus status;
        await using (var fs = File.OpenRead(path))
        {
            (result!, status) = await _audioCacheManager.GetOrCreateOrEmptyAsync(path, fs, waveFormat, category);
        }

        if (status == CacheGetStatus.Failed)
        {
            Logger.Warn("Caching effect failed: " + (File.Exists(path) ? path : "FileNotFound"));
        }
        else if (status == CacheGetStatus.Hit)
        {
            Logger.Info("Got effect cache: " + path);
        }
        else if (status == CacheGetStatus.Created)
        {
            Logger.Info("Cached effect: " + path);
        }

        _playNodeToCachedSoundMapping.TryAdd(hitsoundNode, result!);
        _filenameToCachedSoundMapping.TryAdd(Path.GetFileNameWithoutExtension(path), result!);
    }
}
