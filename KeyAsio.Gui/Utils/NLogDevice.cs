using ATL.Logging;
using Microsoft.Extensions.Logging;

namespace KeyAsio.Gui.Utils;

internal class NLogDevice : ILogDevice
{
    private static ILogger<NLogDevice> _logger;
    private static Log _logInstance = new();

    public static ILogDevice Instance { get; } = new NLogDevice();

    public static void RegisterDefault(ILogger<NLogDevice> logger)
    {
        LogDelegator.SetLog(ref _logInstance);
        _logInstance.Register(Instance);
        _logger = logger;
    }

    private NLogDevice()
    {
        _logInstance.Register(this);
    }

    public void DoLog(Log.LogItem anItem)
    {
        var level = GetLevel(anItem.Level);
        _logger.Log(level, "({Location}) {Message}", anItem.Location, anItem.Message);
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