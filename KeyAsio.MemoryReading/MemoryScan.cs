using System.Diagnostics;
using System.Reflection;
using OsuMemoryDataProvider;
using OsuMemoryDataProvider.OsuMemoryModels;
using ProcessMemoryDataFinder.API;

namespace KeyAsio.MemoryReading;

public static class MemoryScan
{
    //private static readonly ILogger Logger = LogUtils.GetLogger(nameof(RealtimeModeManager));
    private static int _readerInterval;
    private static StructuredOsuMemoryReader _reader;
    private static Task _readTask;
    private static CancellationTokenSource _cts;
    private static bool _isStarted;

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
        _reader.InvalidRead += Reader_InvalidRead;
        _cts = new CancellationTokenSource();
        _readTask = Task.Factory.StartNew(ReadImpl,
            TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach);
    }

    public static async Task StopAsync()
    {
        if (!_isStarted) return;
        await _cts.CancelAsync();
        await _readTask;

        _reader.InvalidRead -= Reader_InvalidRead;
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

    private static bool _canRead = false;
    private static string _baseDirectory;
    private static string? _songsDirectory;

    private static void ReadImpl()
    {
        while (!_cts.IsCancellationRequested)
        {
            if (!_reader.CanRead)
                MemoryReadObject.OsuStatus = OsuMemoryStatus.NotRunning;
            if (!_reader.TryRead(OsuBaseAddresses.BanchoUser))
            {
                //if (_reader.CanRead) Logger.Error($"{nameof(OsuBaseAddresses.BanchoUser)} read failed!");
            }
            else
            {
                MemoryReadObject.PlayerName = OsuBaseAddresses.BanchoUser.Username;
            }

            if (!_reader.TryRead(OsuBaseAddresses.GeneralData))
            {
                //if (_reader.CanRead) Logger.Error($"{nameof(OsuBaseAddresses.GeneralData)} read failed!");
            }
            else
            {
                MemoryReadObject.PlayingTime = OsuBaseAddresses.GeneralData.AudioTime;
                MemoryReadObject.Mods = (Mods)OsuBaseAddresses.GeneralData.Mods;
                if (_reader.CanRead)
                    MemoryReadObject.OsuStatus = OsuBaseAddresses.GeneralData.OsuStatus;
            }

            if (MemoryReadObject.OsuStatus is OsuMemoryStatus.Playing)
            {
                if (!_reader.TryRead(OsuBaseAddresses.Player))
                {
                    //if (_reader.CanRead) Logger.Error($"{nameof(OsuBaseAddresses.Player)} read failed!");
                }
                else
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
                var sb = _reader.GetType().GetField("_memoryReader", BindingFlags.Instance | BindingFlags.NonPublic);
                var f = sb.GetValue(_reader);
                var sb2 = f.GetType().GetField("_memoryReader", BindingFlags.Instance | BindingFlags.NonPublic);
                var f2 = (MemoryReader)sb2.GetValue(f);
                var process = f2.CurrentProcess;
                _baseDirectory = Path.GetDirectoryName(process.MainModule.FileName);
                _songsDirectory = Path.Combine(_baseDirectory, "Songs");
                //var sb3 = f.GetType().GetField("_currentProcess", BindingFlags.Instance | BindingFlags.NonPublic);
                //var f3 = (Process)sb3.GetValue(f2);
            }

            if (!_reader.TryRead(OsuBaseAddresses.Beatmap))
            {
                //if (_reader.CanRead) Logger.Error($"{nameof(OsuBaseAddresses.Beatmap)} read failed!");
            }
            else
            {
                var beatmapFolderName = OsuBaseAddresses.Beatmap.FolderName;
                var beatmapOsuFileName = OsuBaseAddresses.Beatmap.OsuFileName;
                if (beatmapFolderName != null && beatmapOsuFileName != null)
                    MemoryReadObject.BeatmapIdentifier = new BeatmapIdentifier(
                        Path.Combine(_songsDirectory, beatmapFolderName),
                        beatmapOsuFileName);
            }

            _canRead = _reader.CanRead;
            Thread.Sleep(_readerInterval);
        }
    }

    private static void Reader_InvalidRead(object? sender, (object readObject, string propPath) e)
    {
        if (_reader.CanRead) Logger.Error($"Invalid reading {e.readObject?.GetType()}: {e.propPath}");
    }
}