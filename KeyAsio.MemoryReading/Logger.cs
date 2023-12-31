using KeyAsio.MemoryReading.Logging;

namespace KeyAsio.MemoryReading;

public static class Logger
{
    private static ILoggerFactory? _loggerFactory;
    public static void SetLoggerFactory(ILoggerFactory loggerFactory) => _loggerFactory = loggerFactory;

    public static void Info(string message)
    {
        if (_loggerFactory is null) Console.WriteLine(message);
        else _loggerFactory.CreateLogger("KEYAISO_MEM").LogInformation(message);
    }

    public static void Debug(string message)
    {
        if (_loggerFactory is null) Console.WriteLine(message);
        else _loggerFactory.CreateLogger("KEYAISO_MEM").LogDebug(message);
    }

    public static void Error(string message)
    {
        if (_loggerFactory is null) Console.WriteLine(message);
        else _loggerFactory.CreateLogger("KEYAISO_MEM").LogError(message);
    }

    public static void Warn(string message)
    {
        if (_loggerFactory is null) Console.WriteLine(message);
        else _loggerFactory.CreateLogger("KEYAISO_MEM").LogWarning(message);
    }
}