using System.Diagnostics;
using Coosu.Beatmap;
using Coosu.Beatmap.Extensions.Playback;
using Coosu.Beatmap.Sections.GamePlay;
using KeyAsio.Audio;
using KeyAsio.MemoryReading;
using Microsoft.Extensions.Logging;

namespace KeyAsio.Shared.Realtime.Services;

public class PlaySessionService
{
    private readonly ILogger<PlaySessionService> _logger;
    private readonly AudioEngine _audioEngine;
    private readonly HitsoundNodeService _hitsoundNodeService;
    private readonly MusicTrackService _musicTrackService;
    private readonly AudioPlaybackService _audioPlaybackService;
    private readonly AudioCacheService _audioCacheService;

    private readonly Dictionary<GameMode, IAudioProvider> _audioProviderDictionary = new();

    public PlaySessionService(ILogger<PlaySessionService> logger,
        AudioEngine audioEngine,
        HitsoundNodeService hitsoundNodeService,
        MusicTrackService musicTrackService,
        AudioPlaybackService audioPlaybackService,
        AudioCacheService audioCacheService)
    {
        _logger = logger;
        _audioEngine = audioEngine;
        _hitsoundNodeService = hitsoundNodeService;
        _musicTrackService = musicTrackService;
        _audioPlaybackService = audioPlaybackService;
        _audioCacheService = audioCacheService;
        Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.AboveNormal;
    }

    public OsuFile? OsuFile { get; internal set; }
    public string? AudioFilename { get; internal set; }
    public bool IsStarted { get; private set; }

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

    public async Task StartAsync(string beatmapFilenameFull, string beatmapFilename, Mods playMods, int playTime)
    {
        try
        {
            Logger.Info("Start playing.");
            IsStarted = true;
            OsuFile = null;

            var folder = Path.GetDirectoryName(beatmapFilenameFull);
            var previousFolder = _musicTrackService.GetMainTrackFolder();
            if (previousFolder != null && previousFolder != folder)
            {
                Logger.Info("Cleaning caches caused by folder changing.");
                CleanAudioCaches();
            }

            if (folder == null)
            {
                throw new Exception("The beatmap folder is null!");
            }

            var osuFile = await _hitsoundNodeService.InitializeNodeListsAsync(folder, beatmapFilename,
                CurrentAudioProvider, playMods);
            OsuFile = osuFile;
            AudioFilename = osuFile?.General?.AudioFilename;
            _musicTrackService.UpdateMainTrackContext(folder, AudioFilename);
            AddSkinCacheInBackground();
            ResetNodes(playTime);
        }
        catch (Exception ex)
        {
            IsStarted = false;
            _logger.LogError(ex, "Error while starting a beatmap. Filename: {BeatmapFilename}. FilenameReal: {OsuFile}",
                beatmapFilename, OsuFile);
            LogUtils.LogToSentry(MemoryReading.Logging.LogLevel.Error, "Error while starting a beatmap", ex, k =>
            {
                k.SetTag("osu.filename", beatmapFilename);
                k.SetTag("osu.filename_real", OsuFile?.ToString() ?? "");
            });
        }
    }

    public void Stop()
    {
        Logger.Info("Stop playing.");
        IsStarted = false;
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
    }

    private void CleanAudioCaches()
    {
        _audioCacheService.ClearCaches();
    }

    private void ResetNodes(int playTime)
    {
        _hitsoundNodeService.ResetNodes(CurrentAudioProvider, playTime);
    }

    private void AddSkinCacheInBackground()
    {
        var mainFolder = _musicTrackService.GetMainTrackFolder();
        var mainAudioFilename = _musicTrackService.GetMainAudioFilename();
        if (mainFolder == null)
        {
            Logger.Warn("Main track folder is null, stop adding cache.");
            return;
        }

        if (_audioEngine.CurrentDevice == null)
        {
            Logger.Warn($"AudioEngine is null, stop adding cache.");
            return;
        }

        _audioCacheService.SetContext(mainFolder, mainAudioFilename);
        _audioCacheService.PrecacheMusicAndSkinInBackground();
    }
}