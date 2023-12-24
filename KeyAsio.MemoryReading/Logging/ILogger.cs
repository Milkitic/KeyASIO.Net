namespace KeyAsio.MemoryReading.Logging;

public interface ILogger
{
    void Log(LogLevel logLevel, string message);
    void LogInformation(string message);
    void LogDebug(string message);
    void LogError(string message);
    void LogWarning(string message);
    void LogInformation(Exception exception, string message);
    void LogDebug(Exception exception, string message);
    void LogError(Exception exception, string message);
    void LogWarning(Exception exception, string message);
}

public interface ILogger<T> : ILogger;