using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using LogLevel = KeyAsio.MemoryReading.Logging.LogLevel;

namespace KeyAsio.Shared;

internal class WrapperLogger<T> : MemoryReading.Logging.ILogger<T>
{
    private readonly ILogger _innerLogger;
    public WrapperLogger(ILogger innerLogger) => _innerLogger = innerLogger;
    public void Log(LogLevel logLevel, string message) => _innerLogger.Log((Microsoft.Extensions.Logging.LogLevel)logLevel, message);
    public void LogInformation(string message) => _innerLogger.LogInformation(message);
    public void LogDebug(string message) => _innerLogger.LogDebug(message);
    public void LogError(string message) => _innerLogger.LogError(message);
    public void LogWarning(string message) => _innerLogger.LogWarning(message);
    public void LogInformation(Exception exception, string message) => _innerLogger.LogInformation(exception, message);
    public void LogDebug(Exception exception, string message) => _innerLogger.LogDebug(exception, message);
    public void LogError(Exception exception, string message) => _innerLogger.LogError(exception, message);
    public void LogWarning(Exception exception, string message) => _innerLogger.LogWarning(exception, message);
}

internal class WrapperLogger : WrapperLogger<object>
{
    public WrapperLogger(ILogger innerLogger) : base(innerLogger)
    {
    }
}