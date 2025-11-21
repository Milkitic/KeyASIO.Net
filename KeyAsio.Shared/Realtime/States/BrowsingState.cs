using Coosu.Beatmap;
using KeyAsio.MemoryReading;
using KeyAsio.Shared.Realtime.Services;
using OsuMemoryDataProvider;

namespace KeyAsio.Shared.Realtime.States;

public class BrowsingState : IRealtimeState
{
    private readonly AppSettings _appSettings;
    private readonly MusicTrackService _musicTrackService;
    private readonly PlaySessionManager _playSessionManager;

    public BrowsingState(AppSettings appSettings,
        MusicTrackService musicTrackService,
        PlaySessionManager playSessionManager)
    {
        _appSettings = appSettings;
        _musicTrackService = musicTrackService;
        _playSessionManager = playSessionManager;
    }

    public Task EnterAsync(RealtimeProperties ctx, OsuMemoryStatus from)
    {
        _musicTrackService.StartLowPass(200, 16000);
        _musicTrackService.SetResultFlag(false);
        _playSessionManager.Stop();
        return Task.CompletedTask;
    }

    public void Exit(RealtimeProperties ctx, OsuMemoryStatus to)
    {
    }

    public async Task OnPlayTimeChanged(RealtimeProperties ctx, int oldMs, int newMs, bool paused)
    {
        const int selectSongPauseThreshold = 20;
        if (!_appSettings.RealtimeOptions.EnableMusicFunctions) return;

        // Maintain pause state lifecycle for song-select preview
        _musicTrackService.UpdatePauseCount(paused);

        if (_musicTrackService.GetPauseCount() >= selectSongPauseThreshold &&
            _musicTrackService.GetPreviousSelectSongStatus())
        {
            _musicTrackService.PauseCurrentMusic();
            _musicTrackService.SetPreviousSelectSongStatus(false);
        }
        else if (_musicTrackService.GetPauseCount() < selectSongPauseThreshold &&
                 !_musicTrackService.GetPreviousSelectSongStatus())
        {
            _musicTrackService.RecoverCurrentMusic();
            _musicTrackService.SetPreviousSelectSongStatus(true);
        }
    }

    public void OnComboChanged(RealtimeProperties ctx, int oldCombo, int newCombo)
    {
    }

    public void OnBeatmapChanged(RealtimeProperties ctx, BeatmapIdentifier beatmap)
    {
        if (beatmap == default)
        {
            return;
        }

        if (ctx.OsuStatus is not (
            OsuMemoryStatus.SongSelect or
            OsuMemoryStatus.SongSelectEdit or
            OsuMemoryStatus.MainMenu or
            OsuMemoryStatus.MultiplayerSongSelect)
           ) return;

        var coosu = OsuFile.ReadFromFile(beatmap.FilenameFull, k =>
        {
            k.IncludeSection("General");
            k.IncludeSection("Metadata");
        });

        var audioFilePath = coosu.General?.AudioFilename == null
            ? null
            : Path.Combine(beatmap.Folder, coosu.General.AudioFilename);

        if (audioFilePath == _musicTrackService.GetPreviewAudioFilePath())
        {
            return;
        }

        _musicTrackService.UpdatePreviewContext(beatmap.Folder, audioFilePath);
        _musicTrackService.StopCurrentMusic(200);
        _musicTrackService.PlaySingleAudioPreview(coosu, audioFilePath, coosu.General.PreviewTime);
        _musicTrackService.ResetPauseState();
    }

    public void OnModsChanged(RealtimeProperties ctx, Mods oldMods, Mods newMods)
    {
    }
}