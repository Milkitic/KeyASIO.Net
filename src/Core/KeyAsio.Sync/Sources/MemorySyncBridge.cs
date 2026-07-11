using KeyAsio.Configuration;
using System.ComponentModel;
using System.Text;
using KeyAsio.Sync;
using KeyAsio.Common;
using Microsoft.Extensions.Logging;

namespace KeyAsio.Sync.Sources;

public class MemorySyncBridge
{
    private readonly GameSyncSourceCoordinator _sourceCoordinator;
    private readonly StableMemoryGameSyncSource _stableSource;
    private readonly LazerIpcBridge _lazerIpcBridge;
    private readonly SyncSessionContext _syncSessionContext;
    private readonly AppSettings _appSettings;
    private readonly ILogger<MemorySyncBridge> _logger;
    private bool _initialized;
    private bool _isRunning;
    private volatile bool _isStopping;

    public MemorySyncBridge(
        GameSyncSourceCoordinator sourceCoordinator,
        StableMemoryGameSyncSource stableSource,
        LazerIpcBridge lazerIpcBridge,
        SyncSessionContext syncSessionContext,
        AppSettings appSettings,
        ILogger<MemorySyncBridge> logger)
    {
        _sourceCoordinator = sourceCoordinator;
        _stableSource = stableSource;
        _lazerIpcBridge = lazerIpcBridge;
        _syncSessionContext = syncSessionContext;
        _appSettings = appSettings;
        _logger = logger;
    }

    public void Start()
    {
        if (_initialized) return;
        _initialized = true;

        _appSettings.Sync.Scanning.PropertyChanged += OnScanningSettingsChanged;
        _appSettings.Sync.PropertyChanged += OnSyncSettingsChanged;
        _lazerIpcBridge.ChannelConnectionChanged += OnLazerIpcChannelConnectionChanged;

        ConfigureStableSourceIntervals();

        _logger.LogInformation("Initial EnableSync state: {State}", _appSettings.Sync.EnableSync);
        _logger.LogInformation("Initial EnableMixSync state: {State}", _appSettings.Sync.EnableMixSync);

        if (_appSettings.Sync.EnableSync)
        {
            StartScanning();
        }
    }

    private void OnScanningSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AppSettingsSyncScanning.GeneralScanInterval)
            or nameof(AppSettingsSyncScanning.TimingScanInterval))
        {
            ConfigureStableSourceIntervals();
        }
    }

    private void ConfigureStableSourceIntervals()
    {
        _stableSource.ConfigureIntervals(_appSettings.Sync.Scanning.GeneralScanInterval,
            _appSettings.Sync.Scanning.TimingScanInterval);
    }

    private void OnLazerIpcChannelConnectionChanged(LazerIpcChannel channel, bool oldValue, bool newValue)
    {
        if (_isStopping) return;

        _stableSource.SetMemoryScanSuppressed(_lazerIpcBridge.IsAnyChannelConnected);
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
        if (_isRunning) return;

        try
        {
            var player = EncodeUtils.FromBase64String(_appSettings.Logging.PlayerBase64 ?? "", Encoding.ASCII);
            _syncSessionContext.Username = player;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to decode PlayerBase64 string.");
        }

        ConfigureStableSourceIntervals();
        _sourceCoordinator.Start();
        _isRunning = true;
    }

    private async Task StopScanningAsync()
    {
        if (!_isRunning) return;

        _isStopping = true;
        try
        {
            await _sourceCoordinator.StopAsync();
        }
        finally
        {
            _stableSource.SetMemoryScanSuppressed(false);
            _isStopping = false;
            _isRunning = false;
        }
    }
}
