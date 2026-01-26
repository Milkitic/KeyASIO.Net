using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using KeyAsio.Core.Audio;
using KeyAsio.Core.Audio.Caching;
using KeyAsio.Shared.Hitsounds.Playback;
using KeyAsio.Shared.Models;
using KeyAsio.Shared.Services;
using KeyAsio.Shared.Utils;
using Microsoft.Extensions.Logging;
using NAudio.Wave;

namespace KeyAsio.Shared.Sync.Services;

public class GameplayAudioService : IDisposable
{
    private const string BeatmapCacheIdentifier = "default";
    private const string UserCacheIdentifier = "internal";

    private static readonly string[] SkinAudioFiles = ["combobreak"];

    private OsuAudioFileCache _osuAudioFileCache = new();
    private readonly ConcurrentDictionary<PlaybackEvent, CachedAudio> _playNodeToCachedAudioMapping = new();
    private readonly ConcurrentDictionary<string, CachedAudio> _filenameToCachedAudioMapping = new();

    private readonly ILogger<GameplayAudioService> _logger;
    private readonly SyncSessionContext _syncSessionContext;
    private readonly AppSettings _appSettings;
    private readonly IPlaybackEngine _playbackEngine;
    private readonly AudioCacheManager _audioCacheManager;
    private readonly SharedViewModel _sharedViewModel;
    private readonly SkinManager _skinManager;
    private string? _beatmapFolder;
    private string? _audioFilename;

    private readonly AsyncSequentialWorker _cachingWorker;

    public GameplayAudioService(ILogger<GameplayAudioService> logger,
        SyncSessionContext syncSessionContext,
        AppSettings appSettings,
        IPlaybackEngine playbackEngine,
        AudioCacheManager audioCacheManager,
        SharedViewModel sharedViewModel,
        SkinManager skinManager)
    {
        _logger = logger;
        _syncSessionContext = syncSessionContext;
        _appSettings = appSettings;
        _playbackEngine = playbackEngine;
        _audioCacheManager = audioCacheManager;
        _sharedViewModel = sharedViewModel;
        _skinManager = skinManager;

        _cachingWorker = new AsyncSequentialWorker(_logger, "GameplayAudioServiceWorker");
        _sharedViewModel.PropertyChanged += SharedViewModel_PropertyChanged;
    }

    private void SharedViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SharedViewModel.SelectedSkin))
        {
            _logger.LogInformation("Skin changed, clearing gameplay audio service caches.");
            ClearCaches();
        }
    }

    public void SetContext(string? beatmapFolder, string? audioFilename)
    {
        _beatmapFolder = beatmapFolder;
        _audioFilename = audioFilename;
    }

    public void ClearCaches()
    {
        _osuAudioFileCache = new OsuAudioFileCache();
        _audioCacheManager.ClearAll();
        _playNodeToCachedAudioMapping.Clear();
        _filenameToCachedAudioMapping.Clear();
    }

    public bool TryGetAudioByNode(PlaybackEvent node, [NotNullWhen(true)] out CachedAudio? cachedAudio)
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

        if (_playbackEngine.CurrentDevice == null)
        {
            _logger.LogWarning("AudioEngine is null, stop adding cache.");
            return;
        }

        var folder = _beatmapFolder;
        var waveFormat = _playbackEngine.EngineWaveFormat;
        var skinFolder = _sharedViewModel.SelectedSkin?.Folder ?? "";

        _cachingWorker.Enqueue(async token =>
        {
            if (folder != null && _audioFilename != null)
            {
                var musicPath = Path.Combine(folder, _audioFilename);

                var (_, status) = await _audioCacheManager.GetOrCreateOrEmptyFromFileAsync(musicPath, waveFormat);

                if (status == CacheGetStatus.Failed)
                {
                    _logger.LogWarning("Caching music failed: " +
                                       (File.Exists(musicPath) ? musicPath : "FileNotFound"));
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
                foreach (var skinAudioFile in SkinAudioFiles)
                {
                    if (token.IsCancellationRequested) break;
                    await AddSkinCacheAsync(skinAudioFile, folder!, skinFolder, waveFormat);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during skin caching.");
            }
        });
    }

    public void PrecacheHitsoundsRangeInBackground(
        int startTime,
        int endTime,
        IEnumerable<PlaybackEvent> playableNodes,
        [CallerArgumentExpression("playableNodes")]
        string? expression = null)
    {
        if (_beatmapFolder == null)
        {
            _logger.LogWarning("Beatmap folder is null, stop adding cache.");
            return;
        }

        if (_playbackEngine.CurrentDevice == null)
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
        var waveFormat = _playbackEngine.SourceWaveFormat;
        var skinFolder = _sharedViewModel.SelectedSkin?.Folder ?? "";

        _cachingWorker.Enqueue(async token =>
        {
            using var _ = DebugUtils.CreateTimer($"CacheAudio {startTime}~{endTime}", _logger);
            var nodesToCache = playableNodes.Where(k => k.Offset >= startTime && k.Offset < endTime).ToList();

            try
            {
                foreach (var node in nodesToCache)
                {
                    if (token.IsCancellationRequested) break;
                    await AddHitsoundCacheAsync(node, folder!, skinFolder, waveFormat);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during hitsound caching.");
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

        string category;
        var filename = _osuAudioFileCache.GetFileUntilFind(beatmapFolder, filenameWithoutExt, out var resourceOwner);

        CachedAudio result;
        if (resourceOwner == ResourceOwner.UserSkin)
        {
            category = UserCacheIdentifier;
            result = await ResolveAndLoadSkinAudioAsync(filenameWithoutExt, skinFolder, category, waveFormat);
        }
        else
        {
            category = BeatmapCacheIdentifier;
            var path = Path.Combine(beatmapFolder, filename);
            result = await LoadAndCacheAudioAsync(path, category, waveFormat);
        }

        _filenameToCachedAudioMapping.TryAdd(filenameWithoutExt, result);

        return result;
    }

    public async Task AddHitsoundCacheAsync(
        PlaybackEvent playbackEvent,
        string beatmapFolder,
        string skinFolder,
        WaveFormat waveFormat)
    {
        if (!_syncSessionContext.IsStarted)
        {
            _logger.LogWarning("Isn't started, stop adding cache.");
            return;
        }

        if (playbackEvent.Filename == null)
        {
            if (playbackEvent is SampleEvent)
            {
                _logger.LogWarning("Filename is null, add null cache.");
            }

            var cacheResult = await _audioCacheManager.GetOrCreateEmptyAsync("null", waveFormat);
            _playNodeToCachedAudioMapping.TryAdd(playbackEvent, cacheResult.CachedAudio!);
            return;
        }

        string category;
        CachedAudio result;

        if (playbackEvent.ResourceOwner == ResourceOwner.UserSkin)
        {
            category = UserCacheIdentifier;
            result = await ResolveAndLoadSkinAudioAsync(playbackEvent.Filename, skinFolder, category, waveFormat);
        }
        else
        {
            category = BeatmapCacheIdentifier;
            var path = Path.Combine(beatmapFolder, playbackEvent.Filename);
            result = await LoadAndCacheAudioAsync(path, category, waveFormat);
        }

        _playNodeToCachedAudioMapping.TryAdd(playbackEvent, result);
        _filenameToCachedAudioMapping.TryAdd(playbackEvent.Filename, result);
    }

    private async Task<CachedAudio> ResolveAndLoadSkinAudioAsync(string filenameKey, string skinFolder, string category,
        WaveFormat waveFormat)
    {
        var filename = _osuAudioFileCache.GetFileUntilFind(skinFolder, filenameKey, out var resourceOwner);
        if (resourceOwner == ResourceOwner.Beatmap) // Here means file exists in skin folder
        {
            var path = Path.Combine(skinFolder, filename);
            return await LoadAndCacheAudioAsync(path, category, waveFormat);
        }

        if (skinFolder == "{internal}")
        {
            var dynamicKey = $"internal://dynamic/{filenameKey}";
            return _audioCacheManager.CreateDynamic(dynamicKey, waveFormat);
        }

        if (_skinManager.TryGetResource(filenameKey, out var bytes))
        {
            var key = $"internal://{filenameKey}";
            using var stream = new MemoryStream(bytes);
            return await LoadAndCacheAudioFromStreamAsync(key, stream, category, waveFormat);
        }

        _logger.LogWarning("Skin audio not found in skin or resources: {FilenameKey}", filenameKey);
        var empty = await _audioCacheManager.GetOrCreateEmptyAsync(filenameKey, waveFormat, category);
        return empty.CachedAudio!;
    }

    private async Task<CachedAudio> LoadAndCacheAudioFromStreamAsync(string key, Stream stream, string category,
        WaveFormat waveFormat)
    {
        var (result, status) = await _audioCacheManager.GetOrCreateOrEmptyAsync(key, stream, waveFormat, category);
        if (status == CacheGetStatus.Failed)
        {
            _logger.LogWarning("Caching effect failed: {Key}", key);
        }
        else if (status == CacheGetStatus.Hit)
        {
            _logger.LogTrace("Got effect cache: {Key}", key);
        }
        else if (status == CacheGetStatus.Created)
        {
            _logger.LogDebug("Cached effect: {Key}", key);
        }

        return result!;
    }

    private async Task<CachedAudio> LoadAndCacheAudioAsync(string path, string category, WaveFormat waveFormat)
    {
        var (result, status) = await _audioCacheManager.GetOrCreateOrEmptyFromFileAsync(path, waveFormat, category);
        if (status == CacheGetStatus.Failed)
        {
            var file = (File.Exists(path) ? path : "FileNotFound");
            _logger.LogWarning("Caching effect failed: {File}", file);
        }
        else if (status == CacheGetStatus.Hit)
        {
            _logger.LogTrace("Got effect cache: {Path}", path);
        }
        else if (status == CacheGetStatus.Created)
        {
            _logger.LogDebug("Cached effect: {Path}", path);
        }

        return result!;
    }

    public void Dispose()
    {
        _sharedViewModel.PropertyChanged -= SharedViewModel_PropertyChanged;
        _cachingWorker.Dispose();
    }
}