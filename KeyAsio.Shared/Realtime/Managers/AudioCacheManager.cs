using System.Collections;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Coosu.Beatmap.Extensions;
using Coosu.Beatmap.Extensions.Playback;
using KeyAsio.MemoryReading.Logging;
using KeyAsio.Shared.Models;
using KeyAsio.Shared.Utils;
using Milki.Extensions.Configuration;
using Milki.Extensions.MixPlayer.NAudioExtensions.Wave;
using NAudio.Wave;

namespace KeyAsio.Shared.Realtime.Managers;

/// <summary>
/// 音频缓存管理器，负责处理音频文件的缓存逻辑
/// </summary>
public class AudioCacheManager
{
    private static readonly ILogger Logger = LogUtils.GetLogger(nameof(AudioCacheManager));
    private static readonly string[] SkinAudioFiles = ["combobreak"];

    private readonly ConcurrentDictionary<HitsoundNode, CachedSound?> _playNodeToCachedSoundMapping = new();
    private readonly ConcurrentDictionary<string, CachedSound?> _filenameToCachedSoundMapping = new();
    private readonly HitsoundFileCache _hitsoundFileCache = new();

    public AppSettings AppSettings => ConfigurationFactory.GetConfiguration<AppSettings>();

    public bool TryGetAudioByNode(HitsoundNode playableNode, out CachedSound? cachedSound)
    {
        if (!_playNodeToCachedSoundMapping.TryGetValue(playableNode, out cachedSound)) return false;
        return playableNode is not PlayableNode || cachedSound != null;
    }

    public bool TryGetAudioByFilename(string filename, out CachedSound? cachedSound)
    {
        return _filenameToCachedSoundMapping.TryGetValue(filename, out cachedSound);
    }

    public void CleanAudioCaches()
    {
        CachedSoundFactory.ClearCacheSounds();
        _playNodeToCachedSoundMapping.Clear();
        _filenameToCachedSoundMapping.Clear();
    }

    public void AddSkinCacheInBackground(string? folder, string? audioFilename)
    {
        if (folder == null)
        {
            Logger.Warn($"{nameof(folder)} is null, stop adding cache.");
            return;
        }

        if (SharedViewModel.Instance.AudioEngine == null)
        {
            Logger.Warn($"{nameof(SharedViewModel.Instance.AudioEngine)} is null, stop adding cache.");
            return;
        }

        var waveFormat = SharedViewModel.Instance.AudioEngine.WaveFormat;
        var skinFolder = SharedViewModel.Instance.SelectedSkin?.Folder ?? "";
        Task.Run(() =>
        {
            if (folder != null && audioFilename != null)
            {
                var musicPath = Path.Combine(folder, audioFilename);
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

            SkinAudioFiles.AsParallel()
                .WithDegreeOfParallelism(1)
                .ForAll(skinSound =>
                {
                    AddSkinCache(skinSound, folder, skinFolder, waveFormat).Wait();
                });
        });
    }

    public void AddAudioCacheInBackground(int startTime, int endTime,
        IEnumerable<HitsoundNode> playableNodes,
        string? folder,
        bool isStarted,
        [CallerArgumentExpression("playableNodes")]
        string? expression = null)
    {
        if (folder == null)
        {
            Logger.Warn($"{nameof(folder)} is null, stop adding cache.");
            return;
        }

        if (SharedViewModel.Instance.AudioEngine == null)
        {
            Logger.Warn($"{nameof(SharedViewModel.Instance.AudioEngine)} is null, stop adding cache.");
            return;
        }

        if (playableNodes is IList { Count: 0 })
        {
            Logger.Warn($"{expression} has no hitsounds, stop adding cache.");
            return;
        }

        var hitsoundList = playableNodes;
        var waveFormat = SharedViewModel.Instance.AudioEngine.WaveFormat;
        var skinFolder = SharedViewModel.Instance.SelectedSkin?.Folder ?? "";
        Task.Run(() =>
        {
            using var _ = DebugUtils.CreateTimer($"CacheAudio {startTime}~{endTime}", Logger);
            hitsoundList
                .Where(k => k.Offset >= startTime && k.Offset < endTime)
                .AsParallel()
                .WithDegreeOfParallelism(1)
                .ForAll(playableNode =>
                {
                    AddHitsoundCache(playableNode, folder, skinFolder, waveFormat, isStarted).Wait();
                });
        });
    }

    private async Task<CachedSound?> AddSkinCache(string filenameWithoutExt,
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
            filename = _hitsoundFileCache.GetFileUntilFind(skinFolder, filenameWithoutExt,
                out useUserSkin);
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

    private async Task AddHitsoundCache(HitsoundNode hitsoundNode,
        string beatmapFolder,
        string skinFolder,
        WaveFormat waveFormat,
        bool isStarted)
    {
        if (!isStarted)
        {
            Logger.Warn($"Isn't started, stop adding cache.");
            return;
        }

        if (hitsoundNode.Filename == null)
        {
            if (hitsoundNode is PlayableNode)
            {
                Logger.Warn($"Filename is null, add null cache.");
            }

            _playNodeToCachedSoundMapping.TryAdd(hitsoundNode, null);
            return;
        }

        var path = Path.Combine(beatmapFolder, hitsoundNode.Filename);
        string? identifier = null;
        if (hitsoundNode.UseUserSkin)
        {
            identifier = "internal";
            var filename = _hitsoundFileCache.GetFileUntilFind(skinFolder, hitsoundNode.Filename,
                out var useUserSkin);
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