using ATL.Logging;
using KeyAsio.Shared;
using ILogger = KeyAsio.MemoryReading.Logging.ILogger;
using LogLevel = KeyAsio.MemoryReading.Logging.LogLevel;

namespace KeyAsio.Gui.Utils;

internal class NLogDevice : ILogDevice
{
    private static Log _logInstance = new();

    private static readonly ILogger Logger = LogUtils.GetLogger("ATL");
    public static ILogDevice Instance { get; } = new NLogDevice();

    public static void RegisterDefault()
    {
        LogDelegator.SetLog(ref _logInstance);
        _logInstance.Register(Instance);
    }

    private NLogDevice()
    {
        _logInstance.Register(this);
    }

    public void DoLog(Log.LogItem anItem)
    {
        var level = GetLevel(anItem.Level);
        Logger.Log(level, $"({anItem.Location}) {anItem.Message}");
    }

    private static LogLevel GetLevel(int level)
    {
        if (level == Log.LV_DEBUG) return LogLevel.Debug;
        if (level == Log.LV_INFO) return LogLevel.Information;
        if (level == Log.LV_WARNING) return LogLevel.Warning;
        if (level == Log.LV_ERROR) return LogLevel.Error;
        return LogLevel.Information;
    }
}