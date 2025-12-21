using System.Runtime;
using System.Runtime.CompilerServices;
using Coosu.Beatmap;
using Coosu.Beatmap.Extensions.Playback;
using Coosu.Beatmap.Sections.GamePlay;
using KeyAsio.Audio;
using KeyAsio.Plugins.Abstractions;
using KeyAsio.Shared.Plugins;
using Microsoft.Extensions.Logging;

namespace KeyAsio.Shared.Sync.Services;

public class GameplaySessionManager
{
    private readonly ILogger<GameplaySessionManager> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly GameplayAudioService _gameplayAudioService;
    private readonly AudioEngine _audioEngine;
    private readonly SyncSessionContext _syncSessionContext;
    private readonly BeatmapHitsoundLoader _beatmapHitsoundLoader;
    private readonly IPluginManager _pluginManager;
    private readonly SfxPlaybackService _sfxPlaybackService;

    private readonly Dictionary<GameMode, IHitsoundSequencer> _audioProviderDictionary = new();
    private OsuFile? _osuFile;
    private IHitsoundSequencer? _cachedHitsoundSequencer;
    private string? _lastCachedFolder;
    private GCLatencyMode _oldLatencyMode;
    private bool _isLowLatencyModeActive;

    public GameplaySessionManager(ILogger<GameplaySessionManager> logger,
        IServiceProvider serviceProvider,
        GameplayAudioService gameplayAudioService,
        AudioEngine audioEngine,
        SyncSessionContext syncSessionContext,
        BeatmapHitsoundLoader beatmapHitsoundLoader,
        IPluginManager pluginManager,
        SfxPlaybackService sfxPlaybackService)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _gameplayAudioService = gameplayAudioService;
        _audioEngine = audioEngine;
        _syncSessionContext = syncSessionContext;
        _beatmapHitsoundLoader = beatmapHitsoundLoader;
        _pluginManager = pluginManager;
        _sfxPlaybackService = sfxPlaybackService;
    }

    private IMusicManagerPlugin? MusicManager => _pluginManager.GetAllPlugins().OfType<IMusicManagerPlugin>().FirstOrDefault();

    public OsuFile? OsuFile
    {
        get => _osuFile;
        internal set
        {
            _osuFile = value;
            UpdateCachedSequencer();
        }
    }

    public string? AudioFilename { get; internal set; }
    public string? BeatmapFolder { get; private set; }
    public IReadOnlyList<HitsoundNode> PlaybackList => _beatmapHitsoundLoader.PlaybackList;
    public List<PlayableNode> KeyList => _beatmapHitsoundLoader.KeyList;

    public void InitializeProviders(IHitsoundSequencer standardHitsoundSequencer,
        IHitsoundSequencer maniaHitsoundSequencer)
    {
        _audioProviderDictionary[GameMode.Circle] = standardHitsoundSequencer;
        _audioProviderDictionary[GameMode.Taiko] = standardHitsoundSequencer;
        _audioProviderDictionary[GameMode.Catch] = standardHitsoundSequencer;
        _audioProviderDictionary[GameMode.Mania] = maniaHitsoundSequencer;
        UpdateCachedSequencer();
    }

    public IHitsoundSequencer CurrentHitsoundSequencer
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _cachedHitsoundSequencer ?? _audioProviderDictionary[GameMode.Circle];
    }

    private void UpdateCachedSequencer()
    {
        if (_audioProviderDictionary.Count == 0) return;

        if (_osuFile == null)
        {
            _cachedHitsoundSequencer = _audioProviderDictionary[GameMode.Circle];
        }
        else
        {
            _cachedHitsoundSequencer = _audioProviderDictionary.TryGetValue(_osuFile.General.Mode, out var sequencer)
                ? sequencer
                : _audioProviderDictionary[GameMode.Circle];
        }
    }

    public async Task StartAsync(string beatmapFilenameFull, string beatmapFilename)
    {
        try
        {
            _logger.LogInformation("Start playing.");
            OsuFile = null;

            var folder = Path.GetDirectoryName(beatmapFilenameFull);
            if (folder == null)
            {
                throw new Exception("The beatmap folder is null!");
            }

            var osuFile = await _beatmapHitsoundLoader.InitializeNodeListsAsync(folder, beatmapFilename,
                CurrentHitsoundSequencer, _syncSessionContext.PlayMods);
            OsuFile = osuFile;
            AudioFilename = osuFile?.General?.AudioFilename;
            BeatmapFolder = folder;

            PerformCache(folder, AudioFilename);
            //ResetNodes(_syncSessionContext.PlayTime);

            _syncSessionContext.IsStarted = true;

            _oldLatencyMode = GCSettings.LatencyMode;
            GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
            _isLowLatencyModeActive = true;
            _logger.LogInformation("GC LatencyMode set to SustainedLowLatency");
        }
        catch (Exception ex)
        {
            _syncSessionContext.IsStarted = false;
            _logger.LogError(ex, "Error while starting a beatmap. Filename: {BeatmapFilename}. FilenameReal: {OsuFile}",
                beatmapFilename, OsuFile);
            LogUtils.LogToSentry(LogLevel.Error, "Error while starting a beatmap", ex, k =>
            {
                k.SetTag("osu.filename", beatmapFilename);
                k.SetTag("osu.filename_real", OsuFile?.ToString() ?? "");
            });
        }
    }

    private void PerformCache(string newFolder, string? audioFilename)
    {
        if (_lastCachedFolder != null && _lastCachedFolder != newFolder)
        {
            _logger.LogInformation("Cleaning caches caused by folder changing.");
            _gameplayAudioService.ClearCaches();
        }

        _lastCachedFolder = newFolder;

        if (_audioEngine.CurrentDevice == null)
        {
            _logger.LogWarning($"AudioEngine is null, stop adding cache.");
            return;
        }

        _gameplayAudioService.SetContext(newFolder, audioFilename);
        _gameplayAudioService.PrecacheMusicAndSkinInBackground();
    }

    public void Stop()
    {
        if (_isLowLatencyModeActive)
        {
            GCSettings.LatencyMode = _oldLatencyMode;
            _isLowLatencyModeActive = false;
            _logger.LogInformation("GC LatencyMode restored to {Mode}", _oldLatencyMode);
        }

        _logger.LogInformation("Stop playing.");
        _syncSessionContext.IsStarted = false;
        var mixer = _audioEngine.EffectMixer;
        _sfxPlaybackService.ClearAllLoops(mixer);
        MusicManager?.ClearMainTrackAudio();
        mixer?.RemoveAllMixerInputs();

        if (OsuFile != null && BeatmapFolder != null)
        {
            var audioPath = OsuFile.General.AudioFilename == null
                ? null
                : Path.Combine(BeatmapFolder, OsuFile.General.AudioFilename);
            MusicManager?.PlaySingleAudioPreview(OsuFile, audioPath,
                OsuFile.General.PreviewTime);
        }

        _syncSessionContext.BaseMemoryTime = 0;
        _syncSessionContext.Combo = 0;
    }

    private void ResetNodes(int playTime)
    {
        _beatmapHitsoundLoader.ResetNodes(CurrentHitsoundSequencer, playTime);
    }
}