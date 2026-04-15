using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using KeyAsio.Shared.Sync;
using Microsoft.Extensions.Logging;

namespace KeyAsio.Shared.Services;

public sealed class RtssMonitorService : IDisposable
{
    private readonly AppSettings _appSettings;
    private readonly SyncSessionContext _syncSessionContext;
    private readonly ILogger<RtssMonitorService> _logger;

    private RtssOsdWriter? _osdWriter;
    private Task? _updateTask;
    private CancellationTokenSource? _cts;
    private bool _disposed;

    public RtssMonitorService(
        AppSettings appSettings,
        SyncSessionContext syncSessionContext,
        ILogger<RtssMonitorService> logger)
    {
        _appSettings = appSettings;
        _syncSessionContext = syncSessionContext;
        _logger = logger;

        _appSettings.Sync.PropertyChanged += OnSyncSettingsChanged;

        if (_appSettings.Sync.EnableRtssMonitoring)
        {
            StartMonitoring();
        }
    }

    private void OnSyncSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppSettingsSync.EnableRtssMonitoring))
        {
            if (_appSettings.Sync.EnableRtssMonitoring)
            {
                _logger.LogInformation("RTSS monitoring enabled.");
                StartMonitoring();
            }
            else
            {
                _logger.LogInformation("RTSS monitoring disabled.");
                StopMonitoring();
            }
        }
    }

    private void StartMonitoring()
    {
        if (_updateTask != null) return;

        try
        {
            _osdWriter?.Dispose();
            _osdWriter = new RtssOsdWriter("KeyASIO");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize RTSS OSD writer.");
            return;
        }

        _cts = new CancellationTokenSource();
        _updateTask = Task.Factory.StartNew(
            (Action<object>)UpdateLoop,
            _cts.Token,
            _cts.Token,
            TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach,
            TaskScheduler.Default);
    }

    private void StopMonitoring()
    {
        if (_updateTask == null) return;

        _cts?.Cancel();
        try
        {
            _updateTask.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // Ignored
        }

        _cts?.Dispose();
        _cts = null;
        _updateTask = null;

        _osdWriter?.Dispose();
        _osdWriter = null;
    }

    private void UpdateLoop(object? state)
    {
        var token = (CancellationToken)state!;
        var sb = new StringBuilder(1024);
        var stopwatch = Stopwatch.StartNew();
        const int targetIntervalMs = 10; // 100 fps

        while (!token.IsCancellationRequested)
        {
            var frameStart = stopwatch.ElapsedMilliseconds;

            try
            {
                sb.Clear();
                AppendField(sb, "ClientType", _syncSessionContext.ClientType);
                AppendField(sb, "IsStarted", _syncSessionContext.IsStarted);
                AppendField(sb, "IsReplay", _syncSessionContext.IsReplay);
                AppendField(sb, "ProcessId", _syncSessionContext.ProcessId);
                AppendField(sb, "Username", _syncSessionContext.Username ?? "(null)");
                AppendField(sb, "PlayMods", _syncSessionContext.PlayMods);
                AppendField(sb, "PlayTime", _syncSessionContext.PlayTime);
                AppendField(sb, "BaseMemoryTime", _syncSessionContext.BaseMemoryTime);
                AppendField(sb, "Combo", _syncSessionContext.Combo);
                AppendField(sb, "Score", _syncSessionContext.Score);
                AppendField(sb, "OsuStatus", _syncSessionContext.OsuStatus);
                AppendField(sb, "SyncedStatusText", _syncSessionContext.SyncedStatusText);
                AppendField(sb, "Beatmap.Folder", _syncSessionContext.Beatmap.Folder ?? "(null)");
                AppendField(sb, "Beatmap.Filename", _syncSessionContext.Beatmap.Filename ?? "(null)");
                AppendField(sb, "Beatmap.FilenameFull", _syncSessionContext.Beatmap.FilenameFull ?? "(null)");
                AppendField(sb, "LastUpdateTimestamp", _syncSessionContext.LastUpdateTimestamp);

                var text = sb.ToString();
                _osdWriter?.Update(text);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update RTSS OSD.");
            }

            var elapsed = stopwatch.ElapsedMilliseconds - frameStart;
            var delay = targetIntervalMs - (int)elapsed;
            if (delay > 0)
            {
                Thread.Sleep(delay);
            }
        }
    }

    private static void AppendField<T>(StringBuilder sb, string key, T value)
    {
        sb.Append(key).Append(": ").Append(value?.ToString()).Append('\n');
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _appSettings.Sync.PropertyChanged -= OnSyncSettingsChanged;
        StopMonitoring();
    }
}
