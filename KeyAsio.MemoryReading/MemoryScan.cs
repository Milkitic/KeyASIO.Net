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

    private Task? _readTask;
    private CancellationTokenSource? _cts;
    private bool _isStarted;

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

        _sigScan?.Dispose();
        _process?.Dispose();
        _cts.Dispose();

        _isStarted = false;
    }

    private void ReadImpl()
    {
        string? songsDirectory = null;
        var data = new OsuMemoryData();

        while (!_cts!.IsCancellationRequested)
        {
            if (_process == null || _process.HasExited)
            {
                _process?.Dispose();
                _sigScan?.Dispose();
                _process = null;
                _sigScan = null;
                _memoryContext = null;
                songsDirectory = null;

                MemoryReadObject.OsuStatus = OsuMemoryStatus.NotRunning;

                try
                {
                    var processes = Process.GetProcessesByName("osu!");
                    if (processes.Length > 0)
                    {
                        _process = processes[0];
                        _sigScan = new SigScan(_process);
                        _memoryContext = new MemoryContext<OsuMemoryData>(_sigScan, _memoryProfile!);
                        _memoryContext.Scan();

                        var mainModuleFileName = _process.MainModule?.FileName;
                        if (!string.IsNullOrEmpty(mainModuleFileName))
                        {
                            var baseDirectory = Path.GetDirectoryName(mainModuleFileName);
                            if (baseDirectory != null)
                            {
                                songsDirectory = Path.Combine(baseDirectory, "Songs");
                            }
                        }

                        _logger.LogInformation("Connected to osu! process");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error finding osu! process");
                }

                if (_process == null)
                {
                    Thread.Sleep(1000);
                    continue;
                }
            }

            try
            {
                _memoryContext!.BeginUpdate();
                _memoryContext.Populate(data);

                MemoryReadObject.PlayingTime = data.AudioTime;
                MemoryReadObject.OsuStatus = (OsuMemoryStatus)data.RawStatus;
                MemoryReadObject.PlayerName = data.Username;
                MemoryReadObject.Mods = (Mods)data.Mods;

                if (MemoryReadObject.OsuStatus == OsuMemoryStatus.Playing)
                {
                    MemoryReadObject.IsReplay = data.IsReplay;
                    MemoryReadObject.Score = data.Score;
                    MemoryReadObject.Combo = data.Combo;
                }
                else
                {
                    MemoryReadObject.Score = 0;
                    MemoryReadObject.Combo = 0;
                }

                if (songsDirectory != null)
                {
                    var folderName = data.FolderName;
                    var osuFileName = data.OsuFileName;

                    if (!string.IsNullOrEmpty(folderName) && !string.IsNullOrEmpty(osuFileName))
                    {
                        var directory = Path.Combine(songsDirectory, folderName);
                        // Check if changed
                        if (MemoryReadObject.BeatmapIdentifier.Filename != osuFileName ||
                            MemoryReadObject.BeatmapIdentifier.Folder != directory)
                        {
                            MemoryReadObject.BeatmapIdentifier = new BeatmapIdentifier(directory, osuFileName);
                        }
                    }
                }

                Thread.Sleep(_scanInterval);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading memory");
                _process?.Dispose();
                _process = null;
            }
        }
    }
}