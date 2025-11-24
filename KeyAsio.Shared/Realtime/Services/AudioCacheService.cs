using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Coosu.Beatmap.Extensions;
using Coosu.Beatmap.Extensions.Playback;
using KeyAsio.Audio;
using KeyAsio.Audio.Caching;
using KeyAsio.Shared.Models;
using KeyAsio.Shared.Utils;
using Microsoft.Extensions.Logging;
using NAudio.Wave;

namespace KeyAsio.Shared.Realtime.Services;

public class AudioCacheService
{
    private static readonly string[] SkinAudioFiles = ["combobreak"];

    private readonly ParallelOptions _parallelOptions;
    private readonly HitsoundFileCache _hitsoundFileCache = new();
    private readonly ConcurrentDictionary<HitsoundNode, CachedAudio> _playNodeToCachedAudioMapping = new();
    private readonly ConcurrentDictionary<string, CachedAudio> _filenameToCachedAudioMapping = new();

    private readonly ILogger<AudioCacheService> _logger;
    private readonly RealtimeSessionContext _realtimeSessionContext;
    private readonly YamlAppSettings _appSettings;
    private readonly AudioEngine _audioEngine;
    private readonly AudioCacheManager _audioCacheManager;
    private readonly SharedViewModel _sharedViewModel;
    private string? _beatmapFolder;
    private string? _audioFilename;

    public AudioCacheService(ILogger<AudioCacheService> logger,
        RealtimeSessionContext realtimeSessionContext,
        YamlAppSettings appSettings,
        AudioEngine audioEngine,
        AudioCacheManager audioCacheManager,
        SharedViewModel sharedViewModel)
    {
        _logger = logger;
        _realtimeSessionContext = realtimeSessionContext;
        _appSettings = appSettings;
        _audioEngine = audioEngine;
        _audioCacheManager = audioCacheManager;
        _sharedViewModel = sharedViewModel;
        _parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = appSettings.Performance.AudioCacheThreadCount,
        };
    }

    public void SetContext(string? beatmapFolder, string? audioFilename)
    {
        _beatmapFolder = beatmapFolder;
        _audioFilename = audioFilename;
    }

    public void ClearCaches()
    {
        _audioCacheManager.Clear();
        _playNodeToCachedAudioMapping.Clear();
        _filenameToCachedAudioMapping.Clear();
    }

    public bool TryGetAudioByNode(HitsoundNode node, [NotNullWhen(true)] out CachedAudio? cachedAudio)
    {
        if (!_playNodeToCachedAudioMapping.TryGetValue(node, out cachedAudio)) return false;
        return true;
    }

    public bool TryGetCachedAudio(string filenameWithoutExt, out CachedAudio? cachedAudio)
    {
        return _filenameToCachedAudioMapping.TryGetValue(filenameWithoutExt, out cachedAudio);
    }

    public void PrecacheMusicAndSkinInBackground()
    {
        if (_beatmapFolder == null)
        {
            _logger.LogWarning("Beatmap folder is null, stop adding cache.");
            return;
        }

        if (_audioEngine.CurrentDevice == null)
        {
            _logger.LogWarning("AudioEngine is null, stop adding cache.");
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

                var (_, status) = await _audioCacheManager.GetOrCreateOrEmptyFromFileAsync(musicPath, waveFormat);

                if (status == CacheGetStatus.Failed)
                {
                    _logger.LogWarning("Caching music failed: " + (File.Exists(musicPath) ? musicPath : "FileNotFound"));
                }
                else if (status == CacheGetStatus.Hit)
                {
                    _logger.LogTrace("Got music cache: " + musicPath);
                }
                else if (status == CacheGetStatus.Created)
                {
                    _logger.LogDebug("Cached music: " + musicPath);
                }
            }

            try
            {
                await Parallel.ForEachAsync(SkinAudioFiles, _parallelOptions,
                    async (skinAudioFile, _) =>
                    {
                        await AddSkinCacheAsync(skinAudioFile, folder!, skinFolder, waveFormat);
                    });
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Hitsound caching was cancelled.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during parallel hitsound caching.");
            }
        });
    }

    public void PrecacheHitsoundsRangeInBackground(
        int startTime,
        int endTime,
        IEnumerable<HitsoundNode> playableNodes,
        [CallerArgumentExpression("playableNodes")]
        string? expression = null)
    {
        if (_beatmapFolder == null)
        {
            _logger.LogWarning("Beatmap folder is null, stop adding cache.");
            return;
        }

        if (_audioEngine.CurrentDevice == null)
        {
            _logger.LogWarning("AudioEngine is null, stop adding cache.");
            return;
        }

        if (playableNodes is System.Collections.IList { Count: 0 })
        {
            _logger.LogWarning($"{expression} has no hitsounds, stop adding cache.");
            return;
        }

        var folder = _beatmapFolder;
        var waveFormat = _audioEngine.SourceWaveFormat;
        var skinFolder = _sharedViewModel.SelectedSkin?.Folder ?? "";

        Task.Run(async () =>
        {
            using var _ = DebugUtils.CreateTimer($"CacheAudio {startTime}~{endTime}", _logger);
            var nodesToCache = playableNodes.Where(k => k.Offset >= startTime && k.Offset < endTime);

            try
            {
                await Parallel.ForEachAsync(nodesToCache, _parallelOptions, async (node, _) =>
                {
                    await AddHitsoundCacheAsync(node, folder!, skinFolder, waveFormat);
                });
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Hitsound caching was cancelled.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during parallel hitsound caching.");
            }
        });
    }

    public async Task<CachedAudio?> AddSkinCacheAsync(
        string filenameWithoutExt,
        string beatmapFolder,
        string skinFolder,
        WaveFormat waveFormat)
    {
        if (_filenameToCachedAudioMapping.TryGetValue(filenameWithoutExt, out var value)) return value;

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
        (result!, var status) = await _audioCacheManager.GetOrCreateOrEmptyFromFileAsync(path, waveFormat, category);

        if (status == CacheGetStatus.Failed)
        {
            _logger.LogWarning("Caching effect failed: " + (File.Exists(path) ? path : "FileNotFound"));
        }
        else if (status == CacheGetStatus.Hit)
        {
            _logger.LogTrace("Got effect cache: " + path);
        }
        else if (status == CacheGetStatus.Created)
        {
            _logger.LogDebug("Cached effect: " + path);
        }

        _filenameToCachedAudioMapping.TryAdd(filenameWithoutExt, result!);

        return result;
    }

    public async Task AddHitsoundCacheAsync(
        HitsoundNode hitsoundNode,
        string beatmapFolder,
        string skinFolder,
        WaveFormat waveFormat)
    {
        if (!_realtimeSessionContext.IsStarted)
        {
            _logger.LogWarning("Isn't started, stop adding cache.");
            return;
        }

        if (hitsoundNode.Filename == null)
        {
            if (hitsoundNode is PlayableNode)
            {
                _logger.LogWarning("Filename is null, add null cache.");
            }

            var cacheResult = await _audioCacheManager.GetOrCreateEmptyAsync("null", waveFormat);
            _playNodeToCachedAudioMapping.TryAdd(hitsoundNode, cacheResult.CachedAudio!);
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

        var (result, status) = await _audioCacheManager.GetOrCreateOrEmptyFromFileAsync(path, waveFormat, category);

        if (status == CacheGetStatus.Failed)
        {
            _logger.LogWarning("Caching effect failed: " + (File.Exists(path) ? path : "FileNotFound"));
        }
        else if (status == CacheGetStatus.Hit)
        {
            _logger.LogTrace("Got effect cache: " + path);
        }
        else if (status == CacheGetStatus.Created)
        {
            _logger.LogDebug("Cached effect: " + path);
        }

        _playNodeToCachedAudioMapping.TryAdd(hitsoundNode, result!);
        _filenameToCachedAudioMapping.TryAdd(Path.GetFileNameWithoutExtension(path), result!);
    }
}