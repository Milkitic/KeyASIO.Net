﻿using KeyAsio.Shared.OsuMemory;
using KeyAsio.Shared.Sync.Services;

namespace KeyAsio.Shared.Sync.States;

public class NotRunningState : IGameState
{
    private readonly AppSettings _appSettings;
    private readonly BackgroundMusicManager _backgroundMusicManager;

    public NotRunningState(AppSettings appSettings, BackgroundMusicManager backgroundMusicManager)
    {
        _appSettings = appSettings;
        _backgroundMusicManager = backgroundMusicManager;
    }

    public Task EnterAsync(SyncSessionContext ctx, OsuMemoryStatus from)
    {
        if (_appSettings.Sync.EnableMixSync)
        {
            _backgroundMusicManager.StopCurrentMusic(2000);
        }

        return Task.CompletedTask;
    }

    public void Exit(SyncSessionContext ctx, OsuMemoryStatus to)
    {
    }

    public void OnTick(SyncSessionContext ctx, int prevMs, int currMs, bool isPaused)
    {
    }

    public void OnComboChanged(SyncSessionContext ctx, int oldCombo, int newCombo)
    {
    }

    public void OnBeatmapChanged(SyncSessionContext ctx, BeatmapIdentifier beatmap)
    {
    }

    public void OnModsChanged(SyncSessionContext ctx, Mods oldMods, Mods newMods)
    {
    }
}