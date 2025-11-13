using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Coosu.Beatmap.Extensions;
using Coosu.Beatmap.Extensions.Playback;
using KeyAsio.MemoryReading.Logging;
using KeyAsio.Shared.Models;
using KeyAsio.Shared.Utils;
using Milki.Extensions.MixPlayer.NAudioExtensions.Wave;
using NAudio.Wave;

namespace KeyAsio.Shared.Realtime.Services;

public class AudioCacheService
{
    private static readonly ILogger Logger = LogUtils.GetLogger(nameof(AudioCacheService));

    private readonly HitsoundFileCache _hitsoundFileCache = new();
    private readonly ConcurrentDictionary<HitsoundNode, CachedSound?> _playNodeToCachedSoundMapping = new();
    private readonly ConcurrentDictionary<string, CachedSound?> _filenameToCachedSoundMapping = new();

    private static readonly ParallelOptions ParallelOptions = new()
    {
        MaxDegreeOfParallelism = 1, // Preserve use
    };

    private readonly Func<bool> _isStartedProvider;
    private string? _beatmapFolder;
    private string? _audioFilename;

    private static readonly string[] SkinAudioFiles = ["combobreak"];

    public AudioCacheService(Func<bool> isStartedProvider)
    {
        _isStartedProvider = isStartedProvider;
    }

    public void SetContext(string? beatmapFolder, string? audioFilename)
    {
        _beatmapFolder = beatmapFolder;
        _audioFilename = audioFilename;
    }

    public void ClearCaches()
    {
        CachedSoundFactory.ClearCacheSounds();
        _playNodeToCachedSoundMapping.Clear();
        _filenameToCachedSoundMapping.Clear();
    }

    public bool TryGetAudioByNode(HitsoundNode node, out CachedSound? cachedSound)
    {
        if (!_playNodeToCachedSoundMapping.TryGetValue(node, out cachedSound)) return false;
        return node is not PlayableNode || cachedSound != null;
    }

    public bool TryGetCachedSound(string filenameWithoutExt, out CachedSound? cachedSound)
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

        var audioEngine = SharedViewModel.Instance.AudioEngine;
        if (audioEngine == null)
        {
            Logger.Warn("AudioEngine is null, stop adding cache.");
            return;
        }

        var folder = _beatmapFolder;
        var waveFormat = audioEngine.WaveFormat;
        var skinFolder = SharedViewModel.Instance.SelectedSkin?.Folder ?? "";

        Task.Run(async () =>
        {
            if (folder != null && _audioFilename != null)
            {
                var musicPath = Path.Combine(folder, _audioFilename);
                var (result, status) = CachedSoundFactory.GetOrCreateCacheSoundStatus(waveFormat, musicPath).Result;

                if (result == null)
                {
                    Logger.Warn("Caching sound failed: " + (File.Exists(musicPath) ? musicPath : "FileNotFound"));
                }
                else if (status == true)
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

        var audioEngine = SharedViewModel.Instance.AudioEngine;
        if (audioEngine == null)
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
        var waveFormat = audioEngine.WaveFormat;
        var skinFolder = SharedViewModel.Instance.SelectedSkin?.Folder ?? "";

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

    public async Task<CachedSound?> AddSkinCacheAsync(
        string filenameWithoutExt,
        string beatmapFolder,
        string skinFolder,
        WaveFormat waveFormat)
    {
        if (_filenameToCachedSoundMapping.TryGetValue(filenameWithoutExt, out var value)) return value;

        string? identifier = null;
        var filename = _hitsoundFileCache.GetFileUntilFind(beatmapFolder, filenameWithoutExt, out var useUserSkin);
        string path;
        if (useUserSkin)
        {
            identifier = "internal";
            filename = _hitsoundFileCache.GetFileUntilFind(skinFolder, filenameWithoutExt, out useUserSkin);
            path = useUserSkin
                ? Path.Combine(SharedViewModel.Instance.DefaultFolder, $"{filenameWithoutExt}.ogg")
                : Path.Combine(skinFolder, filename);
        }
        else
        {
            path = Path.Combine(beatmapFolder, filename);
        }

        var (result, status) = await CachedSoundFactory
            .GetOrCreateCacheSoundStatus(waveFormat, path, identifier, checkFileExist: false);

        if (result == null)
        {
            Logger.Warn("Caching sound failed: " + (File.Exists(path) ? path : "FileNotFound"));
        }
        else if (status == true)
        {
            Logger.Info("Cached skin audio: " + path);
        }

        _filenameToCachedSoundMapping.TryAdd(filenameWithoutExt, result);

        return result;
    }

    public async Task AddHitsoundCacheAsync(
        HitsoundNode hitsoundNode,
        string beatmapFolder,
        string skinFolder,
        WaveFormat waveFormat)
    {
        if (!_isStartedProvider())
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

            _playNodeToCachedSoundMapping.TryAdd(hitsoundNode, null);
            return;
        }

        var path = Path.Combine(beatmapFolder, hitsoundNode.Filename);
        string? identifier = null;
        if (hitsoundNode.UseUserSkin)
        {
            identifier = "internal";
            var filename = _hitsoundFileCache.GetFileUntilFind(skinFolder, hitsoundNode.Filename, out var useUserSkin);
            path = useUserSkin
                ? Path.Combine(SharedViewModel.Instance.DefaultFolder, $"{hitsoundNode.Filename}.ogg")
                : Path.Combine(skinFolder, filename);
        }

        var (result, status) = await CachedSoundFactory
            .GetOrCreateCacheSoundStatus(waveFormat, path, identifier, checkFileExist: false);

        if (result == null)
        {
            Logger.Warn("Caching sound failed: " + (File.Exists(path) ? path : "FileNotFound"));
        }
        else if (status == true)
        {
            Logger.Info("Cached sound: " + path);
        }

        _playNodeToCachedSoundMapping.TryAdd(hitsoundNode, result);
        _filenameToCachedSoundMapping.TryAdd(Path.GetFileNameWithoutExtension(path), result);
    }
}