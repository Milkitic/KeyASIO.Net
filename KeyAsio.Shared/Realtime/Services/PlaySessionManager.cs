using Coosu.Beatmap;
using Coosu.Beatmap.Extensions.Playback;
using Coosu.Beatmap.Sections.GamePlay;
using KeyAsio.Audio;
using Microsoft.Extensions.Logging;

namespace KeyAsio.Shared.Realtime.Services;

public class PlaySessionManager
{
    private readonly ILogger<PlaySessionManager> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly AudioCacheService _audioCacheService;
    private readonly AudioEngine _audioEngine;
    private readonly RealtimeProperties _realtimeProperties;
    private readonly HitsoundNodeService _hitsoundNodeService;
    private readonly MusicTrackService _musicTrackService;
    private readonly AudioPlaybackService _audioPlaybackService;

    private readonly Dictionary<GameMode, IAudioProvider> _audioProviderDictionary = new();

    public PlaySessionManager(ILogger<PlaySessionManager> logger,
        IServiceProvider serviceProvider,
        AudioCacheService audioCacheService,
        AudioEngine audioEngine,
        RealtimeProperties realtimeProperties,
        HitsoundNodeService hitsoundNodeService,
        MusicTrackService musicTrackService,
        AudioPlaybackService audioPlaybackService)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _audioCacheService = audioCacheService;
        _audioEngine = audioEngine;
        _realtimeProperties = realtimeProperties;
        _hitsoundNodeService = hitsoundNodeService;
        _musicTrackService = musicTrackService;
        _audioPlaybackService = audioPlaybackService;
    }

    public OsuFile? OsuFile { get; internal set; }
    public string? AudioFilename { get; internal set; }

    public IReadOnlyList<HitsoundNode> PlaybackList => _hitsoundNodeService.PlaybackList;
    public List<PlayableNode> KeyList => _hitsoundNodeService.KeyList;

    public void InitializeProviders(IAudioProvider standardAudioProvider, IAudioProvider maniaAudioProvider)
    {
        _audioProviderDictionary[GameMode.Circle] = standardAudioProvider;
        _audioProviderDictionary[GameMode.Taiko] = standardAudioProvider;
        _audioProviderDictionary[GameMode.Catch] = standardAudioProvider;
        _audioProviderDictionary[GameMode.Mania] = maniaAudioProvider;
    }

    public IAudioProvider CurrentAudioProvider
    {
        get
        {
            if (OsuFile == null) return _audioProviderDictionary[GameMode.Circle];
            return _audioProviderDictionary[OsuFile.General.Mode];
        }
    }

    public async Task StartAsync(string beatmapFilenameFull, string beatmapFilename)
    {
        try
        {
            _logger.LogInformation("Start playing.");
            _realtimeProperties.IsStarted = true;
            OsuFile = null;

            var folder = Path.GetDirectoryName(beatmapFilenameFull);
            if (folder == null)
            {
                throw new Exception("The beatmap folder is null!");
            }

            var osuFile = await _hitsoundNodeService.InitializeNodeListsAsync(folder, beatmapFilename,
                CurrentAudioProvider, _realtimeProperties.PlayMods);
            OsuFile = osuFile;
            AudioFilename = osuFile?.General?.AudioFilename;

            var previousFolder = _musicTrackService.GetMainTrackFolder();
            _musicTrackService.UpdateMainTrackContext(folder, AudioFilename);
            PerformCache(previousFolder, folder);
            ResetNodes(_realtimeProperties.PlayTime);
        }
        catch (Exception ex)
        {
            _realtimeProperties.IsStarted = false;
            _logger.LogError(ex, "Error while starting a beatmap. Filename: {BeatmapFilename}. FilenameReal: {OsuFile}",
                beatmapFilename, OsuFile);
            LogUtils.LogToSentry(MemoryReading.Logging.LogLevel.Error, "Error while starting a beatmap", ex, k =>
            {
                k.SetTag("osu.filename", beatmapFilename);
                k.SetTag("osu.filename_real", OsuFile?.ToString() ?? "");
            });
        }
    }

    private void PerformCache(string? previousFolder, string newFolder)
    {
        if (previousFolder != null && previousFolder != newFolder)
        {
            _logger.LogInformation("Cleaning caches caused by folder changing.");
            _audioCacheService.ClearCaches();
        }

        var mainFolder = _musicTrackService.GetMainTrackFolder();
        var mainAudioFilename = _musicTrackService.GetMainAudioFilename();
        if (mainFolder == null)
        {
            _logger.LogWarning("Main track folder is null, stop adding cache.");
            return;
        }

        if (_audioEngine.CurrentDevice == null)
        {
            _logger.LogWarning($"AudioEngine is null, stop adding cache.");
            return;
        }

        _audioCacheService.SetContext(mainFolder, mainAudioFilename);
        _audioCacheService.PrecacheMusicAndSkinInBackground();
    }

    public void Stop()
    {
        _logger.LogInformation("Stop playing.");
        _realtimeProperties.IsStarted = false;
        _musicTrackService.SetFirstStartInitialized(false);
        var mixer = _audioEngine.EffectMixer;
        _audioPlaybackService.ClearAllLoops(mixer);
        _musicTrackService.ClearMainTrackAudio();
        mixer?.RemoveAllMixerInputs();

        if (OsuFile != null)
        {
            _musicTrackService.PlaySingleAudioPreview(OsuFile, _musicTrackService.GetPreviewAudioFilePath(),
                OsuFile.General.PreviewTime);
        }

        _realtimeProperties.PlayTime = 0;
        _realtimeProperties.Combo = 0;
    }

    private void ResetNodes(int playTime)
    {
        _hitsoundNodeService.ResetNodes(CurrentAudioProvider, playTime);
    }
}