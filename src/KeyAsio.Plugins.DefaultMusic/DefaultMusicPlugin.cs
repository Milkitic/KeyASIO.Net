using Coosu.Beatmap;
using KeyAsio.Audio.Caching;
using KeyAsio.Plugins.Abstractions;
using KeyAsio.Plugins.DefaultMusic.Tracks;
using KeyAsio.Shared;
using KeyAsio.Shared.OsuMemory;
using KeyAsio.Shared.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KeyAsio.Plugins.DefaultMusic;

public class DefaultMusicPlugin : ISyncPlugin, IMusicManagerPlugin
{
    public string Id => "KeyAsio.Plugins.DefaultMusic";
    public string Name => "Default MixSync";
    public string Version => "1.0.0";
    public string Author => "KeyAsio Team";
    public string Description => "Provides default music synchronization logic.";

    private IPluginContext? _context;
    private SynchronizedMusicPlayer? _synchronizedMusicPlayer;
    private SongPreviewPlayer? _songPreviewPlayer;

    public void Initialize(IPluginContext context)
    {
        _context = context;
        var sp = context.ServiceProvider;
        var appSettings = sp.GetRequiredService<AppSettings>();

        var syncLogger = sp.GetRequiredService<ILogger<SynchronizedMusicPlayer>>();
        var previewLogger = sp.GetRequiredService<ILogger<SongPreviewPlayer>>();

        _synchronizedMusicPlayer = new SynchronizedMusicPlayer(syncLogger, appSettings, context.AudioEngine);
        _songPreviewPlayer = new SongPreviewPlayer(previewLogger, appSettings, context.AudioEngine);
    }

    public void Startup()
    {
    }

    public void Shutdown()
    {
        _ = _songPreviewPlayer?.StopCurrentMusic();
        _synchronizedMusicPlayer?.ClearAudio();
    }

    public void Unload()
    {
    }

    public void OnSyncStart()
    {
    }

    public void OnSyncStop()
    {
    }

    public void OnTick(ISyncContext context, int deltaMs)
    {
    }

    public void OnStatusChanged(Abstractions.OsuMemoryStatus oldStatus, Abstractions.OsuMemoryStatus newStatus)
    {
    }

    public void OnBeatmapChanged(Abstractions.BeatmapIdentifier beatmap)
    {
    }

    // IMusicManagerPlugin implementation
    public void StartLowPass(int fadeMilliseconds, int targetFrequency)
        => _songPreviewPlayer?.StartLowPass(fadeMilliseconds, targetFrequency);

    public void StopCurrentMusic(int fadeMs = 0)
        => _ = _songPreviewPlayer?.StopCurrentMusic(fadeMs);

    public void PauseCurrentMusic()
        => _ = _songPreviewPlayer?.PauseCurrentMusic();

    public void RecoverCurrentMusic()
        => _ = _songPreviewPlayer?.RecoverCurrentMusic();

    public void PlaySingleAudioPreview(OsuFile osuFile, string? path, int playTime)
        => _ = _songPreviewPlayer?.Play(osuFile, path, playTime);

    public void SetSingleTrackPlayMods(Mods mods)
    {
        if (_synchronizedMusicPlayer != null) _synchronizedMusicPlayer.PlayMods = mods;
    }

    public void SetMainTrackOffsetAndLeadIn(int offset, int leadInMs)
    {
        if (_synchronizedMusicPlayer != null)
        {
            _synchronizedMusicPlayer.Offset = offset;
            _synchronizedMusicPlayer.LeadInMilliseconds = leadInMs;
        }
    }

    public void SyncMainTrackAudio(CachedAudio sound, int positionMs)
        => _synchronizedMusicPlayer?.SyncAudio(sound, positionMs);

    public void ClearMainTrackAudio()
        => _synchronizedMusicPlayer?.ClearAudio();
}