using System.Diagnostics;
using KeyAsio.Memory;
using KeyAsio.Memory.Configuration;
using KeyAsio.Memory.Utils;
using KeyAsio.MemoryReading.OsuMemoryModels;

namespace KeyAsio.MemoryReading;

public static class MemoryScan
{
    private static int _generalInterval;
    private static int _timingInterval;

    private static Process? _process;
    private static SigScan? _sigScan;
    private static MemoryProfile? _memoryProfile;
    private static MemoryContext<OsuMemoryData>? _memoryContext;

    private static string? _songsDirectory;
    private static bool _scanSuccessful;
    private static readonly OsuMemoryData _osuMemoryData = new();

    private static Task? _readTask;
    private static CancellationTokenSource? _cts;
    private static bool _isStarted;
    private static readonly ManualResetEventSlim _intervalUpdatedEvent = new(false);

    public static MemoryReadObject MemoryReadObject { get; } = new();

    public static void Start(int generalInterval, int timingInterval, int processInterval = 500)
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
            Logger.Error("Failed to load memory rules: " + ex.Message);
            throw;
        }

        _cts = new CancellationTokenSource();
        _readTask = Task.Factory.StartNew(ReadImpl,
            TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach);
    }

    public static async Task StopAsync()
    {
        if (!_isStarted) return;
        await _cts!.CancelAsync();

        if (_readTask != null)
            await _readTask;

        CleanupProcess();
        _cts.Dispose();
        _intervalUpdatedEvent.Reset();

        _isStarted = false;
    }

    private static void ReadImpl()
    {
        using var timerScope = new HighPrecisionTimerScope();
        var nextGeneralScan = 0L;
        var nextTimingScan = 0L;
        var stopwatch = Stopwatch.StartNew();

        while (!_cts!.IsCancellationRequested)
        {
            if (!EnsureConnected())
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
                ReadTiming();
                nextTimingScan = now + _timingInterval;
                didWork = true;
            }

            if (now >= nextGeneralScan)
            {
                ReadGeneralData();
                nextGeneralScan = now + _generalInterval;
                didWork = true;
            }

            if (!didWork)
            {
                Thread.Sleep(1);
            }
        }
    }

    private static bool EnsureConnected()
    {
        if (_process is { HasExited: false })
            return true;

        CleanupProcess();

        try
        {
            var processes = Process.GetProcessesByName("osu!");
            if (processes.Length > 0)
            {
                _process = processes[0];
                _sigScan = new SigScan(_process);
                _memoryContext = new MemoryContext<OsuMemoryData>(_sigScan, _memoryProfile!);
                Logger.Info("Connected to osu! process");
                return true;
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Error finding osu! process: " + ex.Message);
        }

        return false;
    }

    private static void CleanupProcess()
    {
        var exiting = _process != null;
        _process?.Dispose();
        _sigScan?.Dispose();

        _process = null;
        _sigScan = null;
        _memoryContext = null;
        _songsDirectory = null;
        _scanSuccessful = false;

        MemoryReadObject.OsuStatus = OsuMemoryStatus.NotRunning;
        MemoryReadObject.PlayingTime = 0;
        MemoryReadObject.BeatmapIdentifier = default;
        if (exiting)
        {
            Logger.Info("Disconnected from osu! process");
            Thread.Sleep(2000);
        }
    }

    private static bool EnsureScanned()
    {
        if (_scanSuccessful) return true;
        if (_memoryContext == null) return false;

        _memoryContext.Scan();
        _memoryContext.BeginUpdate();

        if (_memoryContext.TryGetValue<int>("AudioTime", out _))
        {
            _scanSuccessful = true;
            EnsureSongsDirectory();
            return true;
        }

        _sigScan?.Reload();
        return false;
    }

    private static void EnsureSongsDirectory()
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
            Logger.Error("Error getting osu! main module path: " + ex.Message);
            // Ignore exceptions when accessing MainModule
        }
    }

    private static bool ReadGeneralData()
    {
        try
        {
            _memoryContext!.BeginUpdate();
            _memoryContext.Populate(_osuMemoryData);

            MemoryReadObject.OsuStatus = (OsuMemoryStatus)_osuMemoryData.RawStatus;
            MemoryReadObject.PlayerName = _osuMemoryData.Username;
            MemoryReadObject.Mods = (Mods)_osuMemoryData.Mods;

            if (MemoryReadObject.OsuStatus == OsuMemoryStatus.Playing)
            {
                MemoryReadObject.IsReplay = _osuMemoryData.IsReplay;
                MemoryReadObject.Score = _osuMemoryData.Score;
                MemoryReadObject.Combo = _osuMemoryData.Combo;
            }
            else
            {
                MemoryReadObject.Score = 0;
                MemoryReadObject.Combo = 0;
            }

            if (_songsDirectory != null)
            {
                var folderName = _osuMemoryData.FolderName;
                var osuFileName = _osuMemoryData.OsuFileName;

                if (!string.IsNullOrEmpty(osuFileName))
                {
                    var directory = Path.Combine(_songsDirectory, folderName);
                    if (MemoryReadObject.BeatmapIdentifier.Filename != osuFileName ||
                        MemoryReadObject.BeatmapIdentifier.Folder != directory)
                    {
                        MemoryReadObject.BeatmapIdentifier = new BeatmapIdentifier(directory, osuFileName);
                    }
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            Logger.Error("Error reading memory: " + ex.Message);
            CleanupProcess();
            return false;
        }
    }

    private static void ReadTiming()
    {
        if (_memoryContext != null && _memoryContext.TryGetValue<int>("AudioTime", out var audioTime))
        {
            MemoryReadObject.PlayingTime = audioTime;
        }
    }
}