using System.Diagnostics;
using System.Runtime.CompilerServices;
using KeyAsio.Memory;
using KeyAsio.Memory.Configuration;
using KeyAsio.Memory.Utils;
using Microsoft.Extensions.Logging;

namespace KeyAsio.Shared.OsuMemory;

public class MemoryScan
{
    private readonly ILogger<MemoryScan> _logger;

    private int _generalInterval;
    private int _timingInterval;

    private Process? _process;
    private volatile bool _processExited;
    private SigScan? _sigScan;
    private MemoryProfile? _memoryProfile;
    private MemoryContext<OsuMemoryData>? _memoryContext;

    private string? _songsDirectory;
    private bool _scanSuccessful;
    private readonly OsuMemoryData _osuMemoryData = new();

    private Task? _readTask;
    private CancellationTokenSource? _cts;
    private bool _isStarted;
    private readonly ManualResetEventSlim _intervalUpdatedEvent = new(false);
    private ValueDefinition? _valueDefinition;
    private string? _folderName;

    public MemoryScan(ILogger<MemoryScan> logger)
    {
        _logger = logger;
    }

    public MemoryReadObject MemoryReadObject { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; } = new();

    public void Start(int generalInterval, int timingInterval)
    {
        if (_isStarted) return;
        _isStarted = true;
        _generalInterval = generalInterval;
        _timingInterval = timingInterval;

        try
        {
            // Load from the output directory or adjacent to the assembly
            var assemblyPath = Path.GetDirectoryName(typeof(MemoryScan).Assembly.Location) ?? string.Empty;
            var rulesPath = Path.Combine(assemblyPath, "osu_memory_rules.json");
            _memoryProfile = MemoryProfile.Load(rulesPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load memory rules");
            throw;
        }

        _cts = new CancellationTokenSource();
        _readTask = Task.Factory.StartNew(ReadImpl,
            TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach);
    }

    public async Task StopAsync()
    {
        if (!_isStarted) return;
        await _cts!.CancelAsync();

        if (_readTask != null)
            await _readTask;

        CleanupProcess(MemoryReadObject);
        _cts.Dispose();
        _intervalUpdatedEvent.Reset();

        _isStarted = false;
    }

    public void UpdateIntervals(int generalInterval, int timingInterval)
    {
        _generalInterval = generalInterval;
        _timingInterval = timingInterval;
        _intervalUpdatedEvent.Set();
    }

    private void ReadImpl()
    {
        var memoryReadObject = MemoryReadObject;
        using var timerScope = new HighPrecisionTimerScope();
        var nextGeneralScan = 0L;
        var nextTimingScan = 0L;
        var stopwatch = Stopwatch.StartNew();

        while (!_cts!.IsCancellationRequested)
        {
            if (!EnsureConnected(memoryReadObject))
            {
                Thread.Sleep(500);
                continue;
            }

            if (!EnsureScanned())
            {
                Thread.Sleep(100);
                continue;
            }

            EnsureSongsDirectory();

            long now = stopwatch.ElapsedMilliseconds;
            bool didWork = false;

            if (now >= nextTimingScan)
            {
                ReadTiming(memoryReadObject);
                nextTimingScan = now + _timingInterval;
                didWork = true;
            }

            if (now >= nextGeneralScan)
            {
                ReadGeneralData(memoryReadObject);
                nextGeneralScan = now + _generalInterval;
                didWork = true;
            }

            if (!didWork)
            {
                Thread.Sleep(1);
            }
        }
    }

    private bool EnsureConnected(MemoryReadObject memoryReadObject)
    {
        if (_process != null && !_processExited)
            return true;

        CleanupProcess(memoryReadObject);

        try
        {
            var processes = Process.GetProcessesByName("osu!");
            if (processes.Length > 0)
            {
                _process = processes[0];
                _process.EnableRaisingEvents = true;
                _process.Exited += OnProcessExited;
                _processExited = false;

                if (_process.HasExited)
                {
                    CleanupProcess(memoryReadObject);
                    return false;
                }

                _sigScan = new SigScan(_process);
                _memoryContext = new MemoryContext<OsuMemoryData>(_sigScan, _memoryProfile!);
                if (!_memoryContext.TryGetProfile("AudioTime", out _valueDefinition))
                {
                    _logger.LogWarning("Memory profile is missing required 'AudioTime' definition");
                }

                _logger.LogInformation("Connected to osu! process");
                MemoryReadObject.ProcessId = _process.Id;
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding osu! process");
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void OnProcessExited(object? sender, EventArgs e)
    {
        _processExited = true;
    }

    private void CleanupProcess(MemoryReadObject memoryReadObject)
    {
        var exiting = _process != null;
        if (_process != null)
        {
            _process.Exited -= OnProcessExited;
        }

        _process?.Dispose();
        _sigScan?.Dispose();

        _process = null;
        _sigScan = null;
        _memoryContext = null;
        _songsDirectory = null;
        _scanSuccessful = false;

        _folderName = null;
        MemoryReadObject.OsuStatus = OsuMemoryStatus.NotRunning;
        MemoryReadObject.PlayingTime = 0;
        MemoryReadObject.ProcessId = 0;
        MemoryReadObject.BeatmapIdentifier = default;
        if (exiting)
        {
            _logger.LogInformation("Disconnected from osu! process");
            Thread.Sleep(2000);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool EnsureScanned()
    {
        if (_scanSuccessful) return true;
        if (_memoryContext == null) return false;

        _memoryContext.Scan();
        _memoryContext.BeginUpdate();

        if (_memoryContext.TryGetValueDef<int>(_valueDefinition, out _))
        {
            _scanSuccessful = true;
            EnsureSongsDirectory();
            return true;
        }

        _sigScan?.Reload();
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureSongsDirectory()
    {
        if (_songsDirectory != null) return;

        try
        {
            var mainModuleFileName = _process?.MainModule?.FileName;
            if (string.IsNullOrEmpty(mainModuleFileName)) return;

            var baseDirectory = Path.GetDirectoryName(mainModuleFileName);
            if (baseDirectory != null)
            {
                _songsDirectory = Path.Combine(baseDirectory, "Songs");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting osu! main module path");
            // Ignore exceptions when accessing MainModule
        }
    }

    private bool ReadGeneralData(MemoryReadObject memoryReadObject)
    {
        try
        {
            _memoryContext!.BeginUpdate();
            _memoryContext.Populate(_osuMemoryData);

            memoryReadObject.OsuStatus = (OsuMemoryStatus)_osuMemoryData.RawStatus;
            memoryReadObject.PlayerName = _osuMemoryData.Username;
            memoryReadObject.Mods = (Mods)_osuMemoryData.Mods;

            if (memoryReadObject.OsuStatus == OsuMemoryStatus.Playing)
            {
                memoryReadObject.IsReplay = _osuMemoryData.IsReplay;
                memoryReadObject.Score = _osuMemoryData.Score;
                memoryReadObject.Combo = _osuMemoryData.Combo;
            }
            else
            {
                memoryReadObject.Score = 0;
                memoryReadObject.Combo = 0;
            }

            if (_songsDirectory != null)
            {
                var folderName = _osuMemoryData.FolderName;
                var osuFileName = _osuMemoryData.OsuFileName;

                if (string.IsNullOrEmpty(osuFileName))
                {
                    return true;
                }

                if (memoryReadObject.BeatmapIdentifier.Filename == osuFileName && _folderName == folderName)
                {
                    return true;
                }

                _folderName = folderName;
                var directory = Path.Combine(_songsDirectory, folderName);
                memoryReadObject.BeatmapIdentifier = new BeatmapIdentifier(directory, osuFileName);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading memory");
            CleanupProcess(memoryReadObject);
            return false;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ReadTiming(MemoryReadObject memoryReadObject)
    {
        if (_memoryContext != null && _memoryContext.TryGetValueDef<int>(_valueDefinition, out var audioTime))
        {
            memoryReadObject.PlayingTime = audioTime;
        }
    }
}