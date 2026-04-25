using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using KeyAsio.Plugins.Abstractions;
using KeyAsio.Shared.Sync;
using Microsoft.Extensions.Logging;

namespace KeyAsio.Shared.Services;

public sealed class RtssMonitorService : IDisposable
{
    // RTSS hypertext color tags: we only colorize keys for quick visual scan.
    private const string CriticalKeyColorTag = "<C=FF69B4>";
    private const string ResetColorTag = "<C>";
    private const string BoolTrueColorTag = "<C=52C41A>";
    private const string BoolFalseColorTag = "<C=F5222D>";
    private const string SeparatorColorTag = "<C=8B5A2B>";

    private readonly AppSettings _appSettings;
    private readonly SyncSessionContext _syncSessionContext;
    private readonly ILogger<RtssMonitorService> _logger;

    private RtssOsdWriter? _osdWriter;
    private Task? _updateTask;
    private CancellationTokenSource? _cts;
    private bool _disposed;
    private long _nextFailureLogTimeMs;
    private readonly Queue<int> _hitErrorWindow = new();
    private int _hitErrorWindowSum;
    private int _hitErrorWindowAbsSum;
    private int? _lastHitError;
    private const int HitErrorWindowSize = 64;

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
                AppendField(sb, "File", _syncSessionContext.Beatmap.Filename ?? "(null)");
                AppendLineEnd(sb);

                AppendField(sb, "Folder", _syncSessionContext.Beatmap.Folder ?? "(null)");
                AppendLineEnd(sb);

                AppendField(sb, "PID", _syncSessionContext.ProcessId);
                AppendSeparator(sb);
                AppendField(sb, "Client", _syncSessionContext.ClientType);
                AppendLineEnd(sb);

                AppendField(sb, "User", _syncSessionContext.Username ?? "(null)");
                AppendSeparator(sb);
                AppendField(sb, "Status", _syncSessionContext.OsuStatus);
                AppendLineEnd(sb);

                AppendField(sb, "IsStarted", _syncSessionContext.IsStarted);
                AppendSeparator(sb);
                AppendField(sb, "IsReplay", _syncSessionContext.IsReplay);
                AppendLineEnd(sb);

                AppendCriticalField(sb, "Time", _syncSessionContext.PlayTime);
                AppendSeparator(sb);
                AppendCriticalField(sb, "RawTime", _syncSessionContext.BaseMemoryTime);
                AppendLineEnd(sb);

                AppendCriticalField(sb, "Mods", _syncSessionContext.PlayMods);
                AppendSeparator(sb);
                AppendCriticalField(sb, "Combo", _syncSessionContext.Combo);
                AppendSeparator(sb);
                AppendCriticalField(sb, "Score", _syncSessionContext.Score);
                AppendLineEnd(sb);

                var stats = _syncSessionContext.Statistics;
                AppendCriticalField(sb, "Stats",
                    $"300:{stats.Great} 100:{stats.Ok} 50:{stats.Meh} miss:{stats.Miss} geki:{stats.Perfect} katu:{stats.Good}");
                AppendLineEnd(sb);

                var hitErrors = _syncSessionContext.HitErrors;
                UpdateHitErrorWindow(hitErrors);
                AppendCriticalField(sb, "HitErr", BuildHitErrorSummary(hitErrors));
                AppendLineEnd(sb);

                AppendField(sb, "Update", _syncSessionContext.LastUpdateTimestamp);
                AppendLineEnd(sb);

                var text = sb.ToString();
                _osdWriter?.Update(text);
            }
            catch (Exception ex)
            {
                var now = Environment.TickCount64;
                if (now >= _nextFailureLogTimeMs)
                {
                    _nextFailureLogTimeMs = now + 5000;
                    _logger.LogWarning(ex, "Failed to update RTSS OSD.");
                }
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
        sb.Append(key);
        sb.Append(": ");
        AppendValue(sb, value);
    }

    private static void AppendCriticalField<T>(StringBuilder sb, string key, T value)
    {
        sb.Append(CriticalKeyColorTag)
          .Append(key)
          .Append(ResetColorTag)
          .Append(": ");
        AppendValue(sb, value);
    }

    private static void AppendSeparator(StringBuilder sb)
    {
        sb.Append(SeparatorColorTag)
          .Append(" \t")
          .Append(ResetColorTag);
    }

    private static void AppendLineEnd(StringBuilder sb)
    {
        sb.Append('\n');
    }

    private static void AppendValue<T>(StringBuilder sb, T value)
    {
        if (value is bool boolValue)
        {
            sb.Append(boolValue ? BoolTrueColorTag : BoolFalseColorTag)
              .Append(boolValue ? "true" : "false")
              .Append(ResetColorTag);
            return;
        }

        sb.Append(value?.ToString());
    }

    private void UpdateHitErrorWindow(SyncHitErrors hitErrors)
    {
        var values = hitErrors.Values;
        if (values == null || values.Length == 0) return;

        for (int i = 0; i < values.Length; i++)
        {
            var error = values[i];
            _hitErrorWindow.Enqueue(error);
            _hitErrorWindowSum += error;
            _hitErrorWindowAbsSum += Math.Abs(error);
            _lastHitError = error;

            while (_hitErrorWindow.Count > HitErrorWindowSize)
            {
                var removed = _hitErrorWindow.Dequeue();
                _hitErrorWindowSum -= removed;
                _hitErrorWindowAbsSum -= Math.Abs(removed);
            }
        }
    }

    private string BuildHitErrorSummary(SyncHitErrors hitErrors)
    {
        int delta = hitErrors.Values?.Length ?? 0;
        int count = _hitErrorWindow.Count;
        string lastText = _lastHitError.HasValue ? $"{FormatSigned(_lastHitError.Value)}ms" : "--";
        string avgText = count > 0 ? $"{(double)_hitErrorWindowSum / count:F1}ms" : "--";
        string avgAbsText = count > 0 ? $"{(double)_hitErrorWindowAbsSum / count:F1}ms" : "--";

        return $"idx:{hitErrors.Index} Δ:{delta} last:{lastText} avg:{avgText} abs:{avgAbsText}";
    }

    private static string FormatSigned(int value)
    {
        return value >= 0 ? $"+{value}" : value.ToString();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _appSettings.Sync.PropertyChanged -= OnSyncSettingsChanged;
        StopMonitoring();
    }
}
