using System.Diagnostics;
using System.Runtime.CompilerServices;
using Coosu.Shared.IO;
using KeyAsio.Core.Memory;
using KeyAsio.Core.Memory.Configuration;
using KeyAsio.Core.Memory.Utils;
using KeyAsio.Plugins.Abstractions;
using KeyAsio.Plugins.Abstractions.OsuMemory;
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
    private long _timingScanGeneration;
    private readonly ManualResetEventSlim _intervalUpdatedEvent = new(false);
    private ValueDefinition? _valueDefinition;
    private ValueDefinition? _comboValueDefinition;
    private ValueDefinition? _scoreCuttingEdgeValueDefinition;
    private ValueDefinition? _scoreLegacyValueDefinition;
    private ValueDefinition? _hit100ValueDefinition;
    private ValueDefinition? _hit300ValueDefinition; 
    private ValueDefinition? _hit50ValueDefinition;
    private ValueDefinition? _hitGekiValueDefinition;
    private ValueDefinition? _hitKatuValueDefinition;
    private ValueDefinition? _hitMissValueDefinition;

    private string? _folderName;

    public MemoryScan(ILogger<MemoryScan> logger)
    {
        _logger = logger;
    }

    public MemoryReadObject MemoryReadObject
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
    } = new();

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
        // WARN: Single threaded reading to avoid thread pool switching
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

    public void ReloadRules()
    {
        try
        {
            var assemblyPath = Path.GetDirectoryName(typeof(MemoryScan).Assembly.Location) ?? string.Empty;
            var rulesPath = Path.Combine(assemblyPath, "osu_memory_rules.json");
            _memoryProfile = MemoryProfile.Load(rulesPath);

            // Force reconnection to rebuild MemoryContext with new profile
            CleanupProcess(MemoryReadObject);

            _logger.LogInformation("Memory rules reloaded.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reload memory rules");
        }
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
                ReadScore(memoryReadObject);
                ReadCombo(memoryReadObject);
                memoryReadObject.TimingScanGeneration = ++_timingScanGeneration;
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

                try
                {
                    var uptime = DateTime.Now - _process.StartTime;
                    if (uptime.TotalSeconds < 6)
                    {
                        _logger.LogInformation(
                            "osu! process detected early (uptime {Uptime:F1}s). Delaying connection for 10s...",
                            uptime.TotalSeconds);
                        for (var i = 0; i < 60; i++)
                        {
                            if (_cts?.IsCancellationRequested == true) return false;
                            Thread.Sleep(100);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to check process uptime");
                }

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

                if (!_memoryContext.TryGetProfile("Combo", out _comboValueDefinition))
                {
                    _logger.LogWarning("Memory profile is missing required 'Combo' definition");
                }

                _memoryContext.TryGetProfile("ScoreCuttingEdge", out _scoreCuttingEdgeValueDefinition);
                _memoryContext.TryGetProfile("ScoreLegacy", out _scoreLegacyValueDefinition);

                _memoryContext.TryGetProfile("Hit100", out _hit100ValueDefinition);
                _memoryContext.TryGetProfile("Hit300", out _hit300ValueDefinition);
                _memoryContext.TryGetProfile("Hit50", out _hit50ValueDefinition);
                _memoryContext.TryGetProfile("HitGeki", out _hitGekiValueDefinition);
                _memoryContext.TryGetProfile("HitKatu", out _hitKatuValueDefinition);
                _memoryContext.TryGetProfile("HitMiss", out _hitMissValueDefinition);

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
        _scoreCuttingEdgeValueDefinition = null;
        _scoreLegacyValueDefinition = null;
        _comboValueDefinition = null;
        _hit100ValueDefinition = null;
        _hit300ValueDefinition = null;
        _hit50ValueDefinition = null;
        _hitGekiValueDefinition = null;
        _hitKatuValueDefinition = null;
        _hitMissValueDefinition = null;

        _folderName = null;
        _timingScanGeneration = 0;
        memoryReadObject.OsuStatus = OsuMemoryStatus.NotRunning;
        memoryReadObject.PlayingTime = 0;
        memoryReadObject.TimingScanGeneration = 0;
        memoryReadObject.ProcessId = 0;
        memoryReadObject.BeatmapIdentifier = default;
        memoryReadObject.Score = 0;
        memoryReadObject.Statistics = SyncStatistics.Empty;
        memoryReadObject.HitErrors = SyncHitErrors.Empty;
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
            if (baseDirectory == null) return;

            var beatmapDirectory = "Songs";
            var configPath = Path.Combine(baseDirectory, $"osu!.{Environment.UserName}.cfg");

            if (File.Exists(configPath))
            {
                try
                {
                    using var sr = new StreamReader(configPath);
                    using var lineReader = new EphemeralLineReader(sr);
                    while (lineReader.ReadLine() is { } memory)
                    {
                        var span = memory.Span.Trim();
                        if (span.StartsWith('#')) continue;
                        if (span.IsEmpty) continue;

                        var commentIndex = span.IndexOf('#');
                        var validSpan = commentIndex == -1 ? span : span.Slice(0, commentIndex).TrimEnd();

                        var splitterIndex = span.IndexOf('=');
                        if (splitterIndex == -1) continue;

                        var key = validSpan.Slice(0, splitterIndex).TrimEnd();
                        var value = validSpan.Slice(splitterIndex + 1).TrimStart();

                        if (key.Equals("BeatmapDirectory", StringComparison.OrdinalIgnoreCase))
                        {
                            beatmapDirectory = value.ToString();
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to read osu! configuration file");
                }
            }
            else
            {
                _logger.LogWarning("osu! configuration file not found at {ConfigPath}", configPath);
            }

            _songsDirectory = Path.IsPathRooted(beatmapDirectory)
                ? beatmapDirectory
                : Path.Combine(baseDirectory, beatmapDirectory);
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
                ReadScore(memoryReadObject);
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

        if (memoryReadObject.OsuStatus != OsuMemoryStatus.Playing)
        {
            memoryReadObject.Statistics = SyncStatistics.Empty;
            memoryReadObject.HitErrors = SyncHitErrors.Empty;
            return;
        }

        memoryReadObject.Statistics = new SyncStatistics(
            ReadStat(_hitGekiValueDefinition),
            ReadStat(_hit300ValueDefinition),
            ReadStat(_hitKatuValueDefinition),
            ReadStat(_hit100ValueDefinition),
            ReadStat(_hit50ValueDefinition),
            ReadStat(_hitMissValueDefinition));

        memoryReadObject.HitErrors = ReadHitErrors(memoryReadObject.HitErrors.Index);
    }


    private bool ReadCombo(MemoryReadObject memoryReadObject)
    {
        if (memoryReadObject.OsuStatus == OsuMemoryStatus.Playing &&
            _memoryContext != null &&
            _memoryContext.TryGetValueDef<ushort>(_comboValueDefinition, out var combo))
        {
            memoryReadObject.Combo = combo;
            return true;
        }

        return false;
    }

    private void ReadScore(MemoryReadObject memoryReadObject)
    {
        if (memoryReadObject.OsuStatus != OsuMemoryStatus.Playing || _memoryContext == null)
        {
            memoryReadObject.Score = 0;
            return;
        }

        var score = _osuMemoryData.Score;
        var preferredScoreDef = _scoreLegacyValueDefinition;
        if (_memoryContext.TryGetString("OsuVersion", out var osuVersion) &&
            !string.IsNullOrEmpty(osuVersion) &&
            (osuVersion.Contains("cuttingedge", StringComparison.OrdinalIgnoreCase) ||
             osuVersion.Contains("tourney", StringComparison.OrdinalIgnoreCase)))
        {
            preferredScoreDef = _scoreCuttingEdgeValueDefinition;
        }

        if (preferredScoreDef != null &&
            _memoryContext.TryGetValueDef<int>(preferredScoreDef, out var preferredScore) &&
            preferredScore > 0)
        {
            score = preferredScore;
        }
        else if (score <= 0 &&
                 _scoreLegacyValueDefinition != null &&
                 _memoryContext.TryGetValueDef<int>(_scoreLegacyValueDefinition, out var legacyScore) &&
                 legacyScore > 0)
        {
            score = legacyScore;
        }
        else if (score <= 0 &&
                 _scoreCuttingEdgeValueDefinition != null &&
                 _memoryContext.TryGetValueDef<int>(_scoreCuttingEdgeValueDefinition, out var cuttingEdgeScore) &&
                 cuttingEdgeScore > 0)
        {
            score = cuttingEdgeScore;
        }

        memoryReadObject.Score = score;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int ReadStat(ValueDefinition? definition)
    {
        return _memoryContext != null && _memoryContext.TryGetValueDef<short>(definition, out var value)
            ? value
            : 0;
    }

    private SyncHitErrors ReadHitErrors(int lastIndex)
    {
        int safeLastIndex = Math.Max(0, lastIndex);

        if (_memoryContext == null || _sigScan == null || _comboValueDefinition == null)
        {
            return new SyncHitErrors(safeLastIndex, []);
        }

        var scoreBase = _memoryContext.ResolveBaseAddress(_comboValueDefinition);
        if (scoreBase == IntPtr.Zero)
        {
            return new SyncHitErrors(safeLastIndex, []);
        }

        if (!MemoryReadHelper.TryGetPointer(_sigScan, scoreBase + 0x38, out var hitErrorsListBase) ||
            hitErrorsListBase == IntPtr.Zero)
        {
            return new SyncHitErrors(safeLastIndex, []);
        }

        if (!MemoryReadHelper.TryGetPointer(_sigScan, hitErrorsListBase + 0x4, out var itemsBase) ||
            itemsBase == IntPtr.Zero)
        {
            return new SyncHitErrors(safeLastIndex, []);
        }

        if (!MemoryReadHelper.TryGetValue<int>(_sigScan, hitErrorsListBase + 0xc, out var size) ||
            size is < 0 or > 100_000)
        {
            return new SyncHitErrors(safeLastIndex, []);
        }

        if (safeLastIndex > size)
        {
            safeLastIndex = 0;
        }

        int count = size - safeLastIndex;
        if (count <= 0)
        {
            return new SyncHitErrors(size, []);
        }

        var errors = new int[count];
        int readCount = 0;
        int index = safeLastIndex;
        for (int i = safeLastIndex; i < size; i++)
        {
            if (!MemoryReadHelper.TryGetValue<int>(_sigScan, itemsBase + 0x8 + 0x4 * i, out var error))
            {
                break;
            }

            if (error is < -10_000 or > 10_000)
            {
                _logger.LogDebug("Strange value in hitErrors: {HitError}", error);
                break;
            }

            errors[readCount++] = error;
            index = i + 1;
        }

        if (readCount != errors.Length)
        {
            Array.Resize(ref errors, readCount);
        }

        return new SyncHitErrors(index, errors);
    }
}
