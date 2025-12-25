﻿using System.ComponentModel;
using System.Text;
using KeyAsio.Plugins.Abstractions.OsuMemory;
using KeyAsio.Shared.Sync;
using KeyAsio.Shared.Utils;
using Microsoft.Extensions.Logging;

namespace KeyAsio.Shared.OsuMemory;

public class MemorySyncBridge
{
    private readonly MemoryScan _memoryScan;
    private readonly SyncSessionContext _syncSessionContext;
    private readonly AppSettings _appSettings;
    private readonly ILogger<MemorySyncBridge> _logger;
    private bool _initialized;

    public MemorySyncBridge(
        MemoryScan memoryScan,
        SyncSessionContext syncSessionContext,
        AppSettings appSettings,
        ILogger<MemorySyncBridge> logger)
    {
        _memoryScan = memoryScan;
        _syncSessionContext = syncSessionContext;
        _appSettings = appSettings;
        _logger = logger;
    }

    public void Start()
    {
        if (_initialized) return;
        _initialized = true;

        BindEvents();

        _appSettings.Sync.Scanning.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(AppSettingsSyncScanning.GeneralScanInterval)
                or nameof(AppSettingsSyncScanning.TimingScanInterval))
            {
                _memoryScan.UpdateIntervals(_appSettings.Sync.Scanning.GeneralScanInterval,
                    _appSettings.Sync.Scanning.TimingScanInterval);
            }
        };

        _appSettings.Sync.PropertyChanged += OnSyncSettingsChanged;

        _logger.LogInformation("Initial EnableSync state: {State}", _appSettings.Sync.EnableSync);
        _logger.LogInformation("Initial EnableMixSync state: {State}", _appSettings.Sync.EnableMixSync);

        if (_appSettings.Sync.EnableSync)
        {
            StartScanning();
        }
    }

    private void OnSyncSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppSettingsSync.EnableSync))
        {
            if (_appSettings.Sync.EnableSync)
            {
                _logger.LogInformation("Memory Sync enabled.");
                StartScanning();
            }
            else
            {
                _logger.LogInformation("Memory Sync disabled.");
                _ = StopScanningAsync();
            }
        }
        else if (e.PropertyName == nameof(AppSettingsSync.EnableMixSync))
        {
            _logger.LogInformation("EnableMixSync changed to: {State}", _appSettings.Sync.EnableMixSync);
        }
    }

    private void StartScanning()
    {
        try
        {
            var player = EncodeUtils.FromBase64String(_appSettings.Logging.PlayerBase64 ?? "", Encoding.ASCII);
            _syncSessionContext.Username = player;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to decode PlayerBase64 string.");
        }

        _memoryScan.Start(_appSettings.Sync.Scanning.GeneralScanInterval,
            _appSettings.Sync.Scanning.TimingScanInterval);
    }

    private async Task StopScanningAsync()
    {
        await _memoryScan.StopAsync();
    }

    private void BindEvents()
    {
        _memoryScan.MemoryReadObject.PlayerNameChanged += OnPlayerNameChanged;
        _memoryScan.MemoryReadObject.ModsChanged += OnModsChanged;
        _memoryScan.MemoryReadObject.ComboChanged += OnComboChanged;
        _memoryScan.MemoryReadObject.ScoreChanged += OnScoreChanged;
        _memoryScan.MemoryReadObject.IsReplayChanged += OnIsReplayChanged;
        _memoryScan.MemoryReadObject.BeatmapIdentifierChanged += OnBeatmapIdentifierChanged;
        _memoryScan.MemoryReadObject.OsuStatusChanged += OnOsuStatusChanged;
        _memoryScan.MemoryReadObject.ProcessIdChanged += OnProcessIdChanged;
        _memoryScan.MemoryReadObject.PlayingTimeChanged += OnPlayingTimeChanged;
    }

    private void OnPlayerNameChanged(string? oldName, string? newName)
    {
        try
        {
            _syncSessionContext.Username = newName;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update SyncSessionContext.Username");
        }
    }

    private void OnModsChanged(Mods oldMods, Mods newMods)
    {
        try
        {
            _syncSessionContext.PlayMods = newMods;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update SyncSessionContext.PlayMods");
        }
    }

    private void OnIsReplayChanged(bool oldIsReplay, bool newIsReplay)
    {
        try
        {
            _syncSessionContext.IsReplay = newIsReplay;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update SyncSessionContext.IsReplay");
        }
    }

    private void OnBeatmapIdentifierChanged(BeatmapIdentifier oldBeatmap, BeatmapIdentifier newBeatmap)
    {
        try
        {
            _syncSessionContext.Beatmap = newBeatmap;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update SyncSessionContext.Beatmap");
        }
    }

    private void OnOsuStatusChanged(OsuMemoryStatus oldStatus, OsuMemoryStatus newStatus)
    {
        try
        {
            _syncSessionContext.OsuStatus = newStatus;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update SyncSessionContext.OsuStatus");
        }
    }

    private void OnProcessIdChanged(int oldId, int newId)
    {
        try
        {
            _syncSessionContext.ProcessId = newId;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update SyncSessionContext.ProcessId");
        }
    }

    private void OnPlayingTimeChanged(int oldTime, int newTime)
    {
        try
        {
            _syncSessionContext.BaseMemoryTime = newTime;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update SyncSessionContext.BaseMemoryTime");
        }
    }

    private void OnComboChanged(int oldCombo, int newCombo)
    {
        try
        {
            _syncSessionContext.Combo = newCombo;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update SyncSessionContext.Combo");
        }
    }

    private void OnScoreChanged(int oldScore, int newScore)
    {
        try
        {
            _syncSessionContext.Score = newScore;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update SyncSessionContext.Score");
        }
    }
}