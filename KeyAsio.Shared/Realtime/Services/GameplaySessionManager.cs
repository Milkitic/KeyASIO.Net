using Coosu.Beatmap;
using Coosu.Beatmap.Extensions.Playback;
using Coosu.Beatmap.Sections.GamePlay;
using KeyAsio.Audio;
using Microsoft.Extensions.Logging;

namespace KeyAsio.Shared.Realtime.Services;

public class GameplaySessionManager
{
    private readonly ILogger<GameplaySessionManager> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly AudioCacheService _audioCacheService;
    private readonly AudioEngine _audioEngine;
    private readonly RealtimeSessionContext _realtimeSessionContext;
    private readonly BeatmapHitsoundLoader _beatmapHitsoundLoader;
    private readonly BackgroundMusicManager _backgroundMusicManager;
    private readonly SfxPlaybackService _sfxPlaybackService;

    private readonly Dictionary<GameMode, IHitsoundSequencer> _audioProviderDictionary = new();

    public GameplaySessionManager(ILogger<GameplaySessionManager> logger,
        IServiceProvider serviceProvider,
        AudioCacheService audioCacheService,
        AudioEngine audioEngine,
        RealtimeSessionContext realtimeSessionContext,
        BeatmapHitsoundLoader beatmapHitsoundLoader,
        BackgroundMusicManager backgroundMusicManager,
        SfxPlaybackService sfxPlaybackService)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _audioCacheService = audioCacheService;
        _audioEngine = audioEngine;
        _realtimeSessionContext = realtimeSessionContext;
        _beatmapHitsoundLoader = beatmapHitsoundLoader;
        _backgroundMusicManager = backgroundMusicManager;
        _sfxPlaybackService = sfxPlaybackService;
    }

    public OsuFile? OsuFile { get; internal set; }
    public string? AudioFilename { get; internal set; }

    public IReadOnlyList<HitsoundNode> PlaybackList => _beatmapHitsoundLoader.PlaybackList;
    public List<PlayableNode> KeyList => _beatmapHitsoundLoader.KeyList;

    public void InitializeProviders(IHitsoundSequencer standardHitsoundSequencer, IHitsoundSequencer maniaHitsoundSequencer)
    {
        _audioProviderDictionary[GameMode.Circle] = standardHitsoundSequencer;
        _audioProviderDictionary[GameMode.Taiko] = standardHitsoundSequencer;
        _audioProviderDictionary[GameMode.Catch] = standardHitsoundSequencer;
        _audioProviderDictionary[GameMode.Mania] = maniaHitsoundSequencer;
    }

    public IHitsoundSequencer CurrentHitsoundSequencer
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
            _realtimeSessionContext.IsStarted = true;
            OsuFile = null;

            var folder = Path.GetDirectoryName(beatmapFilenameFull);
            if (folder == null)
            {
                throw new Exception("The beatmap folder is null!");
            }

            var osuFile = await _beatmapHitsoundLoader.InitializeNodeListsAsync(folder, beatmapFilename,
                CurrentHitsoundSequencer, _realtimeSessionContext.PlayMods);
            OsuFile = osuFile;
            AudioFilename = osuFile?.General?.AudioFilename;

            var previousFolder = _backgroundMusicManager.GetMainTrackFolder();
            _backgroundMusicManager.UpdateMainTrackContext(folder, AudioFilename);
            PerformCache(previousFolder, folder);
            ResetNodes(_realtimeSessionContext.PlayTime);
        }
        catch (Exception ex)
        {
            _realtimeSessionContext.IsStarted = false;
            _logger.LogError(ex, "Error while starting a beatmap. Filename: {BeatmapFilename}. FilenameReal: {OsuFile}",
                beatmapFilename, OsuFile);
            LogUtils.LogToSentry(LogLevel.Error, "Error while starting a beatmap", ex, k =>
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

        var mainFolder = _backgroundMusicManager.GetMainTrackFolder();
        var mainAudioFilename = _backgroundMusicManager.GetMainAudioFilename();
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
        _realtimeSessionContext.IsStarted = false;
        _backgroundMusicManager.SetFirstStartInitialized(false);
        var mixer = _audioEngine.EffectMixer;
        _sfxPlaybackService.ClearAllLoops(mixer);
        _backgroundMusicManager.ClearMainTrackAudio();
        mixer?.RemoveAllMixerInputs();

        if (OsuFile != null)
        {
            _backgroundMusicManager.PlaySingleAudioPreview(OsuFile, _backgroundMusicManager.GetPreviewAudioFilePath(),
                OsuFile.General.PreviewTime);
        }

        _realtimeSessionContext.PlayTime = 0;
        _realtimeSessionContext.Combo = 0;
    }

    private void ResetNodes(int playTime)
    {
        _beatmapHitsoundLoader.ResetNodes(CurrentHitsoundSequencer, playTime);
    }
}