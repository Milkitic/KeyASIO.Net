using Coosu.Beatmap;
using KeyAsio.Audio.Caching;
using KeyAsio.Plugins.Abstractions;
using KeyAsio.Shared.OsuMemory;
using KeyAsio.Shared.Plugins;
using Microsoft.Extensions.Logging;

namespace KeyAsio.Shared.Sync.Services;

public class BackgroundMusicManager
{
    private readonly ILogger<BackgroundMusicManager> _logger;
    private readonly IPluginManager _pluginManager;
    private IMusicManagerPlugin? _musicManager;

    public BackgroundMusicManager(ILogger<BackgroundMusicManager> logger, IPluginManager pluginManager)
    {
        _logger = logger;
        _pluginManager = pluginManager;
    }

    public bool FirstStartInitialized { get; set; }

    private IMusicManagerPlugin? MusicManager
    {
        get
        {
            if (_musicManager != null) return _musicManager;
            _musicManager = _pluginManager.GetAllPlugins().OfType<IMusicManagerPlugin>().FirstOrDefault();
            return _musicManager;
        }
    }

    public void StartLowPass(int fadeMilliseconds, int targetFrequency)
        => MusicManager?.StartLowPass(fadeMilliseconds, targetFrequency);

    public void StopCurrentMusic(int fadeMs = 0)
        => MusicManager?.StopCurrentMusic(fadeMs);

    public void PauseCurrentMusic()
        => MusicManager?.PauseCurrentMusic();

    public void RecoverCurrentMusic()
        => MusicManager?.RecoverCurrentMusic();

    public void PlaySingleAudioPreview(OsuFile osuFile, string? path, int playTime)
        => MusicManager?.PlaySingleAudioPreview(osuFile, path, playTime);

    public void SetSingleTrackPlayMods(Mods mods)
        => MusicManager?.SetSingleTrackPlayMods(mods);

    public void SetMainTrackOffsetAndLeadIn(int offset, int leadInMs)
        => MusicManager?.SetMainTrackOffsetAndLeadIn(offset, leadInMs);

    public void SyncMainTrackAudio(CachedAudio sound, int positionMs)
        => MusicManager?.SyncMainTrackAudio(sound, positionMs);

    public void ClearMainTrackAudio()
        => MusicManager?.ClearMainTrackAudio();
}