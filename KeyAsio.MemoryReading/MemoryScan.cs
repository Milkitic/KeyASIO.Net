using System.Diagnostics;
using KeyAsio.Memory;
using KeyAsio.Memory.Configuration;
using KeyAsio.MemoryReading.OsuMemoryModels;
using Microsoft.Extensions.Logging;

namespace KeyAsio.MemoryReading;

public class MemoryScan
{
    private readonly ILogger<MemoryScan> _logger;

    private int _scanInterval;

    private Process? _process;
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

    public MemoryScan(ILogger<MemoryScan> logger)
    {
        _logger = logger;
    }

    public MemoryReadObject MemoryReadObject { get; } = new();

    public void Start(int scanInterval, int processInterval = 500)
    {
        if (_isStarted) return;
        _isStarted = true;
        _scanInterval = scanInterval;

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

        CleanupProcess();
        _cts.Dispose();
        _intervalUpdatedEvent.Reset();

        _isStarted = false;
    }

    public void UpdateScanInterval(int scanInterval)
    {
        _scanInterval = scanInterval;
        _intervalUpdatedEvent.Set();
    }

    private void ReadImpl()
    {
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

            if (ReadData())
            {
                WaitHandle.WaitAny([_cts.Token.WaitHandle, _intervalUpdatedEvent.WaitHandle], _scanInterval);
                if (_intervalUpdatedEvent.IsSet) _intervalUpdatedEvent.Reset();
            }
        }
    }

    private bool EnsureConnected()
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

    private void CleanupProcess()
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
        MemoryReadObject.ProcessId = 0;
        MemoryReadObject.BeatmapIdentifier = default;
        if (exiting)
        {
            _logger.LogInformation("Disconnected from osu! process");
            Thread.Sleep(2000);
        }
    }

    private bool EnsureScanned()
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

    private bool ReadData()
    {
        try
        {
            _memoryContext!.BeginUpdate();
            _memoryContext.Populate(_osuMemoryData);

            MemoryReadObject.PlayingTime = _osuMemoryData.AudioTime;
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
            _logger.LogError(ex, "Error reading memory");
            CleanupProcess();
            return false;
        }
    }
}