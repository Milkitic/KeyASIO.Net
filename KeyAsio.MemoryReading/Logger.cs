using KeyAsio.MemoryReading.Logging;

namespace KeyAsio.MemoryReading;

public static class Logger
{
    private static ILoggerFactory _loggerFactory;
    public static void SetLoggerFactory(ILoggerFactory loggerFactory) => _loggerFactory = loggerFactory;

    public static void Info(string message)
    {
        if (_loggerFactory is null) Console.WriteLine(message);
        else _loggerFactory.CreateLogger("ORTDP").LogInformation(message);
    }

    public static void Debug(string message)
    {
        if (_loggerFactory is null) Console.WriteLine(message);
        else _loggerFactory.CreateLogger("ORTDP").LogDebug(message);
    }

    public static void Error(string message)
    {
        if (_loggerFactory is null) Console.WriteLine(message);
        else _loggerFactory.CreateLogger("ORTDP").LogError(message);
    }

    public static void Warn(string message)
    {
        if (_loggerFactory is null) Console.WriteLine(message);
        else _loggerFactory.CreateLogger("ORTDP").LogWarning(message);
    }
}