using System.Diagnostics;
using System.Reflection;
using OsuMemoryDataProvider;
using OsuMemoryDataProvider.OsuMemoryModels;
using ProcessMemoryDataFinder.API;

namespace KeyAsio.MemoryReading;

public static class MemoryScan
{
    private const string FieldMemoryReader1 = "_memoryReader";
    private const string FieldMemoryReader2 = "_memoryReader";

    private static int _readerInterval;
    private static StructuredOsuMemoryReader? _reader;
    private static Task? _readTask;
    private static CancellationTokenSource? _cts;
    private static bool _isStarted;

    private static bool _canRead = false;
    private static string? _baseDirectory;
    private static string? _songsDirectory;
    private static MemoryReader? _innerMemoryReader;

    private static readonly OsuBaseAddresses OsuBaseAddresses = new();

    public static MemoryReadObject MemoryReadObject { get; } = new();

    public static void Start(int readerInterval, int processInterval = 500)
    {
        if (_isStarted) return;
        _isStarted = true;
        _readerInterval = readerInterval;
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
        _canRead = false;
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
        var sw = Stopwatch.StartNew();
        while (!_cts!.IsCancellationRequested)
        {
            if (sw.Elapsed.TotalMilliseconds > _readerInterval)
            {
                sw.Restart();
                if (!_reader!.CanRead)
                    MemoryReadObject.OsuStatus = OsuMemoryStatus.NotRunning;
                if (_reader.TryRead(OsuBaseAddresses.BanchoUser))
                {
                    MemoryReadObject.PlayerName = OsuBaseAddresses.BanchoUser.Username;
                }

                if (_reader.TryRead(OsuBaseAddresses.GeneralData))
                {
                    MemoryReadObject.PlayingTime = OsuBaseAddresses.GeneralData.AudioTime;
                    MemoryReadObject.Mods = (Mods)OsuBaseAddresses.GeneralData.Mods;
                    if (_reader.CanRead)
                        MemoryReadObject.OsuStatus = OsuBaseAddresses.GeneralData.OsuStatus;
                }

                if (MemoryReadObject.OsuStatus is OsuMemoryStatus.Playing)
                {
                    if (_reader.TryRead(OsuBaseAddresses.Player))
                    {
                        MemoryReadObject.Combo = OsuBaseAddresses.Player.Combo;
                        MemoryReadObject.Score = OsuBaseAddresses.Player.Score;
                    }
                }
                else
                {
                    MemoryReadObject.Combo = 0;
                    MemoryReadObject.Score = 0;
                }

                if (_canRead != _reader.CanRead && _reader.CanRead)
                {
                    var process = _innerMemoryReader!.CurrentProcess;
                    _baseDirectory = Path.GetDirectoryName(process.MainModule!.FileName);
                    _songsDirectory = Path.Combine(_baseDirectory!, "Songs");
                }

                if (_reader.TryRead(OsuBaseAddresses.Beatmap))
                {
                    var beatmapFolderName = OsuBaseAddresses.Beatmap.FolderName;
                    var beatmapOsuFileName = OsuBaseAddresses.Beatmap.OsuFileName;
                    if (beatmapFolderName != null && beatmapOsuFileName != null)
                    {
                        var directory = Path.Combine(_songsDirectory!, beatmapFolderName);
                        MemoryReadObject.BeatmapIdentifier = new BeatmapIdentifier(directory, beatmapOsuFileName);
                    }
                }

                _canRead = _reader.CanRead;
            }

            Thread.Sleep(1);
        }
    }

    private static void Reader_InvalidRead(object? sender, (object readObject, string propPath) e)
    {
        if (_reader is { CanRead: true }) Logger.Error($"Invalid reading {e.readObject?.GetType()}: {e.propPath}");
    }
}