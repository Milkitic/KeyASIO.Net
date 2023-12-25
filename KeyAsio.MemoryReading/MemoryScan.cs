using System.Diagnostics;
using System.Reflection;
using KeyAsio.MemoryReading.OsuMemoryModels;
using KeyAsio.MemoryReading.OsuMemoryModels.Direct;
using OsuMemoryDataProvider;
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
        _reader = new StructuredOsuMemoryReader
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
        var generalSw = Stopwatch.StartNew();
        var timingSw = Stopwatch.StartNew();

        var general = new OsuBaseAddresses();
        var slim = new GeneralDataSlim();

        var canRead = false;
        string? songsDirectory = null;

        while (!_cts!.IsCancellationRequested)
        {
            if (timingSw.Elapsed.TotalMilliseconds > _timingScanInterval)
            {
                timingSw.Restart();

                if (_reader!.TryRead(slim))
                {
                    MemoryReadObject.PlayingTime = slim.AudioTime;
                }
            }

            if (generalSw.Elapsed.TotalMilliseconds > _generalScanInterval)
            {
                generalSw.Restart();
                if (!_reader!.CanRead)
                {
                    MemoryReadObject.OsuStatus = OsuMemoryStatus.NotRunning;
                }

                if (_reader.TryRead(general.BanchoUser))
                {
                    MemoryReadObject.PlayerName = general.BanchoUser.Username;
                }

                if (_reader.TryRead(general.GeneralData))
                {
                    MemoryReadObject.Mods = (Mods)general.GeneralData.Mods;
                    if (_reader.CanRead)
                    {
                        MemoryReadObject.OsuStatus = general.GeneralData.OsuStatus;
                    }
                }

                if (MemoryReadObject.OsuStatus is OsuMemoryStatus.Playing)
                {
                    if (_reader.TryRead(general.Player))
                    {
                        MemoryReadObject.Combo = general.Player.Combo;
                        MemoryReadObject.Score = general.Player.Score;
                        MemoryReadObject.IsReplay = general.Player.IsReplay;
                    }
                }
                else
                {
                    MemoryReadObject.Combo = 0;
                    MemoryReadObject.Score = 0;
                }

                if (canRead != _reader!.CanRead)
                {
                    if (_reader.CanRead)
                    {
                        var baseDirectory = Path.GetDirectoryName(_innerMemoryReader!.CurrentProcess.MainModule!.FileName);
                        songsDirectory = Path.Combine(baseDirectory!, "Songs");
                    }

                    canRead = _reader.CanRead;
                }

                if (_reader.TryRead(general.Beatmap))
                {
                    var beatmapFolderName = general.Beatmap.FolderName;
                    var beatmapOsuFileName = general.Beatmap.OsuFileName;
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

    private static void Reader_InvalidRead(object? sender, (object readObject, string propPath) e)
    {
        if (_reader is { CanRead: true }) Logger.Error($"Invalid reading {e.readObject?.GetType()}: {e.propPath}");
    }
}