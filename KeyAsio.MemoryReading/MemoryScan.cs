using System.Diagnostics;
using System.Reflection;
using KeyAsio.MemoryReading.OsuMemoryModels;
using KeyAsio.MemoryReading.OsuMemoryModels.Direct;
using OsuMemoryDataProvider;
using ProcessMemoryDataFinder;
using ProcessMemoryDataFinder.API;

namespace KeyAsio.MemoryReading;

public static class MemoryScan
{
    private const string FieldMemoryReader1 = "_memoryReader";
    private const string FieldMemoryReader2 = "_memoryReader";

    private static int _timingScanInterval;
    private static int _generalScanInterval;
    private static StructuredOsuMemoryReader? _reader;
    private static Task? _readTask;
    private static CancellationTokenSource? _cts;
    private static bool _isStarted;

    private static MemoryReader? _innerMemoryReader;

    public static MemoryReadObject MemoryReadObject { get; } = new();

    public static void Start(int generalScanInterval, int timingScanInterval, int processInterval = 500)
    {
        if (_isStarted) return;
        _isStarted = true;
        _generalScanInterval = generalScanInterval;
        _timingScanInterval = timingScanInterval;
        _reader = new StructuredOsuMemoryReader(new ProcessTargetOptions("osu!"))
        {
            ProcessWatcherDelayMs = processInterval
        };

        var type1 = _reader.GetType();
        var fieldMemoryReader1 = type1.GetField(FieldMemoryReader1, BindingFlags.Instance | BindingFlags.NonPublic);
        if (fieldMemoryReader1 == null)
        {
            throw new ArgumentNullException(FieldMemoryReader1, $"Could not find internal field of {type1.Name}");
        }

        var memoryReader = fieldMemoryReader1.GetValue(_reader);
        if (memoryReader == null)
        {
            throw new ArgumentNullException(FieldMemoryReader1, $"Internal field of {type1.Name} is null.");
        }

        var type2 = memoryReader.GetType();
        var fieldMemoryReader2 = type2.GetField(FieldMemoryReader2, BindingFlags.Instance | BindingFlags.NonPublic);
        if (fieldMemoryReader2 == null)
        {
            throw new ArgumentNullException(FieldMemoryReader2, $"Could not find internal field of {type2.FullName}");
        }

        _innerMemoryReader = (MemoryReader?)fieldMemoryReader2.GetValue(memoryReader);
        if (_innerMemoryReader == null)
        {
            throw new ArgumentNullException(FieldMemoryReader1, $"Internal field of {type2.Name} is null.");
        }

        _reader.InvalidRead += Reader_InvalidRead;
        _cts = new CancellationTokenSource();
        _readTask = Task.Factory.StartNew(ReadImpl,
            TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach);
    }

    public static async Task StopAsync()
    {
        if (!_isStarted) return;
        await _cts!.CancelAsync();
        await _readTask!;

        _reader!.InvalidRead -= Reader_InvalidRead;
        await CastAndDispose(_reader);
        await CastAndDispose(_readTask);
        await CastAndDispose(_cts);

        _isStarted = false;
        return;

        static ValueTask CastAndDispose(IDisposable resource)
        {
            if (resource is IAsyncDisposable resourceAsyncDisposable)
                return resourceAsyncDisposable.DisposeAsync();
            resource.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    private static void ReadImpl()
    {
        var generalScanLimiter = Stopwatch.StartNew();
        var timingScanLimiter = Stopwatch.StartNew();

        var internalTimerScanLimiter = Stopwatch.StartNew();
        var internalTimer = new Stopwatch();

        var allData = new OsuBaseAddresses();
        var audioTimeData = new GeneralDataSlim();

        var canRead = false;
        string? songsDirectory = null;
        int lastFetchPlayingTime = 0;

        while (!_cts!.IsCancellationRequested)
        {
            var ratio = GetRatio(allData.GeneralData.OsuStatus, allData.GeneralData.Mods);
            var playingTime = lastFetchPlayingTime + internalTimer.ElapsedMilliseconds * ratio;
            if (timingScanLimiter.Elapsed.TotalMilliseconds > _timingScanInterval)
            {
                timingScanLimiter.Restart();

                if (_reader!.TryRead(audioTimeData))
                {
                    if (audioTimeData.AudioTime == lastFetchPlayingTime)
                    {
                        MemoryReadObject.PlayingTime = audioTimeData.AudioTime;
                        internalTimer.Reset();
                    }
                    else
                    {
                        MemoryReadObject.PlayingTime = audioTimeData.AudioTime;
                        lastFetchPlayingTime = audioTimeData.AudioTime;
                        internalTimer.Restart();
                    }
                }
                else if (_timingScanInterval >= 16 && internalTimer.IsRunning &&
                         internalTimerScanLimiter.ElapsedMilliseconds > 16)
                {
                    internalTimerScanLimiter.Restart();
                    MemoryReadObject.PlayingTime = lastFetchPlayingTime;
                    internalTimer.Reset();
                }
            }
            else if (_timingScanInterval >= 16 && internalTimer.IsRunning &&
                     internalTimerScanLimiter.ElapsedMilliseconds > 16)
            {
                internalTimerScanLimiter.Restart();
                MemoryReadObject.PlayingTime = (int)playingTime;
            }

            if (generalScanLimiter.Elapsed.TotalMilliseconds > _generalScanInterval)
            {
                generalScanLimiter.Restart();
                if (!_reader!.CanRead)
                {
                    MemoryReadObject.OsuStatus = OsuMemoryStatus.NotRunning;
                }

                if (_reader.TryRead(allData.BanchoUser))
                {
                    MemoryReadObject.PlayerName = allData.BanchoUser.Username;
                }

                if (_reader.TryRead(allData.GeneralData))
                {
                    MemoryReadObject.Mods = (Mods)allData.GeneralData.Mods;
                    if (_reader.CanRead)
                    {
                        MemoryReadObject.OsuStatus = allData.GeneralData.OsuStatus;
                    }
                }

                if (MemoryReadObject.OsuStatus is OsuMemoryStatus.Playing)
                {
                    if (_reader.TryRead(allData.Player))
                    {
                        MemoryReadObject.IsReplay = allData.Player.IsReplay;
                        MemoryReadObject.Score = allData.Player.Score;
                        MemoryReadObject.Combo = allData.Player.Combo;
                    }
                }
                else
                {
                    MemoryReadObject.Score = 0;
                    MemoryReadObject.Combo = 0;
                }

                if (canRead != _reader.CanRead)
                {
                    if (_reader.CanRead)
                    {
                        var baseDirectory =
                            Path.GetDirectoryName(_innerMemoryReader!.CurrentProcess.MainModule!.FileName);
                        songsDirectory = Path.Combine(baseDirectory!, "Songs");
                    }

                    canRead = _reader.CanRead;
                }

                if (_reader.TryRead(allData.Beatmap))
                {
                    var beatmapFolderName = allData.Beatmap.FolderName;
                    var beatmapOsuFileName = allData.Beatmap.OsuFileName;
                    if (beatmapFolderName != null && beatmapOsuFileName != null)
                    {
                        var directory = Path.Combine(songsDirectory!, beatmapFolderName);
                        MemoryReadObject.BeatmapIdentifier = new BeatmapIdentifier(directory, beatmapOsuFileName);
                    }
                }
            }

            Thread.Sleep(1);
        }
    }

    private static double GetRatio(OsuMemoryStatus osuStatus, int rawMods)
    {
        if (osuStatus != OsuMemoryStatus.Playing) return 1;
        var mods = (Mods)rawMods;

        if ((mods & Mods.DoubleTime) != 0) return 1.5;
        if ((mods & Mods.HalfTime) != 0) return 0.75;
        return 1;
    }

    private static void Reader_InvalidRead(object? sender, (object readObject, string propPath) e)
    {
        if (_reader is { CanRead: true }) Logger.Error($"Invalid reading {e.readObject?.GetType()}: {e.propPath}");
    }
}