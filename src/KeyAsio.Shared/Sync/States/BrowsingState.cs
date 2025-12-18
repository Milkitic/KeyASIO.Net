﻿using Coosu.Beatmap;
using KeyAsio.Shared.OsuMemory;
using KeyAsio.Shared.Sync.Services;

namespace KeyAsio.Shared.Sync.States;

public class BrowsingState : IGameState
{
    private readonly AppSettings _appSettings;
    private readonly BackgroundMusicManager _backgroundMusicManager;
    private readonly GameplaySessionManager _gameplaySessionManager;

    public BrowsingState(AppSettings appSettings,
        BackgroundMusicManager backgroundMusicManager,
        GameplaySessionManager gameplaySessionManager)
    {
        _appSettings = appSettings;
        _backgroundMusicManager = backgroundMusicManager;
        _gameplaySessionManager = gameplaySessionManager;
    }

    public Task EnterAsync(SyncSessionContext ctx, OsuMemoryStatus from)
    {
        _backgroundMusicManager.StartLowPass(200, 16000);
        _backgroundMusicManager.SetResultFlag(false);
        _gameplaySessionManager.Stop();
        return Task.CompletedTask;
    }

    public void Exit(SyncSessionContext ctx, OsuMemoryStatus to)
    {
    }

    public void OnPlayTimeChanged(SyncSessionContext ctx, int oldMs, int newMs, bool paused)
    {
        const int selectSongPauseThreshold = 20;
        if (!_appSettings.Sync.EnableMixSync) return;

        // Maintain pause state lifecycle for song-select preview
        _backgroundMusicManager.UpdatePauseCount(paused);

        if (_backgroundMusicManager.GetPauseCount() >= selectSongPauseThreshold &&
            _backgroundMusicManager.GetPreviousSelectSongStatus())
        {
            _backgroundMusicManager.PauseCurrentMusic();
            _backgroundMusicManager.SetPreviousSelectSongStatus(false);
        }
        else if (_backgroundMusicManager.GetPauseCount() < selectSongPauseThreshold &&
                 !_backgroundMusicManager.GetPreviousSelectSongStatus())
        {
            _backgroundMusicManager.RecoverCurrentMusic();
            _backgroundMusicManager.SetPreviousSelectSongStatus(true);
        }
    }

    public void OnComboChanged(SyncSessionContext ctx, int oldCombo, int newCombo)
    {
    }

    public void OnBeatmapChanged(SyncSessionContext ctx, BeatmapIdentifier beatmap)
    {
        if (beatmap == default || string.IsNullOrEmpty(beatmap.Folder))
        {
            return;
        }

        if (ctx.OsuStatus is not (
            OsuMemoryStatus.SongSelection or
            OsuMemoryStatus.EditSongSelection or
            OsuMemoryStatus.MainView or
            OsuMemoryStatus.MultiSongSelection)
           ) return;

        if (!File.Exists(beatmap.FilenameFull)) return;

        var coosu = OsuFile.ReadFromFile(beatmap.FilenameFull, k =>
        {
            k.IncludeSection("General");
            k.IncludeSection("Metadata");
        });

        var audioFilePath = coosu.General?.AudioFilename == null
            ? null
            : Path.Combine(beatmap.Folder, coosu.General.AudioFilename);

        if (audioFilePath == _backgroundMusicManager.GetPreviewAudioFilePath())
        {
            return;
        }

        _backgroundMusicManager.UpdatePreviewContext(beatmap.Folder, audioFilePath);
        _backgroundMusicManager.StopCurrentMusic(200);
        _backgroundMusicManager.PlaySingleAudioPreview(coosu, audioFilePath, coosu.General.PreviewTime);
        _backgroundMusicManager.ResetPauseState();
    }

    public void OnModsChanged(SyncSessionContext ctx, Mods oldMods, Mods newMods)
    {
    }
}